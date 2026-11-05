using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMinuteWorkspace;

public sealed record GetCouncilMinuteWorkspaceQuery(Guid AssignmentId);

public sealed class GetCouncilMinuteWorkspaceHandler(
    ICouncilRepository councilRepository,
    ICouncilUserInfoReader councilUserInfoReader,
    IDefenseGroupContract defenseGroupContract,
    ITopicDefenseContract topicDefenseContract,
    ICurrentUser currentUser)
{
    public async Task<Result<CouncilMinuteWorkspaceResponseDto>> HandleAsync(
        GetCouncilMinuteWorkspaceQuery query,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<CouncilMinuteWorkspaceResponseDto>(CouncilErrors.Forbidden);
        }

        var assignment = await councilRepository.GetAssignmentWithCouncilAndMembersAsync(query.AssignmentId, ct);
        if (assignment == null)
        {
            return Result.Failure<CouncilMinuteWorkspaceResponseDto>(CouncilErrors.AssignmentNotFound);
        }

        var council = assignment.Council;
        var members = council.Members.ToList();

        // Lấy thông tin user
        var memberUserIds = members.Select(m => m.UserId).Distinct().ToList();
        var activeReviewer = await councilRepository.GetActiveReviewerAssignmentByGroupIdAsync(assignment.GroupId, ct);
        if (activeReviewer != null)
        {
            memberUserIds.Add(activeReviewer.ReviewerId);
        }

        var groups = await defenseGroupContract.GetGroupsByIdsAsync([assignment.GroupId], ct);
        groups.TryGetValue(assignment.GroupId, out var groupSnapshot);
        var topicId = groupSnapshot?.TopicId ?? Guid.Empty;
        var topics = await topicDefenseContract.GetTopicsDetailsAsync([topicId], ct);
        topics.TryGetValue(topicId, out var topic);

        if (topic != null)
        {
            memberUserIds.Add(topic.AdvisorId);
        }

        var userDict = await councilUserInfoReader.GetUsersByIdsAsync(memberUserIds.Distinct().ToList(), ct);

        // Lấy chủ tịch & thư ký
        var chairman = members.FirstOrDefault(m => m.RoleInCouncil == CouncilMemberRole.Chairman);
        var secretary = members.FirstOrDefault(m => m.RoleInCouncil == CouncilMemberRole.Secretary);

        string? chairmanName = null;
        if (chairman != null && userDict.TryGetValue(chairman.UserId, out var cmUser))
        {
            chairmanName = cmUser.FullName;
        }

        string? secretaryName = null;
        if (secretary != null && userDict.TryGetValue(secretary.UserId, out var secUser))
        {
            secretaryName = secUser.FullName;
        }

        // Lấy thông tin CouncilMinute hiện tại
        var minute = assignment.Minute ?? await councilRepository.GetMinuteByAssignmentIdAsync(assignment.Id, ct);
        var lockStatus = minute?.LockStatus.ToString() ?? "Open";
        var studentFeedback = minute?.RequiredRevisions ?? minute?.GeneralFeedback;

        var groupCode = $"N{assignment.OrderNumber:D2}";
        var assignmentInfo = new MinuteWorkspaceAssignmentInfoDto(
            assignment.Id,
            council.Id,
            council.CouncilCode,
            assignment.GroupId,
            groupCode,
            chairmanName,
            secretaryName,
            lockStatus,
            studentFeedback);

        // Lấy các điểm thành phần: Advisor, Reviewer, CouncilScores
        var advisorEval = await councilRepository.GetAdvisorEvaluationByGroupIdAsync(assignment.GroupId, ct);
        var councilScores = await councilRepository.GetCouncilScoresWithDetailsByAssignmentIdAsync(assignment.Id, ct);
        var scoreMap = councilScores.ToDictionary(s => s.CouncilMemberId);

        var isReviewerInCouncil = activeReviewer != null && members.Any(m => m.UserId == activeReviewer.ReviewerId);

        string advisorName = "GVHD";
        if (topic != null && userDict.TryGetValue(topic.AdvisorId, out var advUser))
        {
            advisorName = advUser.FullName;
        }

        string reviewerName = "GVPB";
        if (activeReviewer != null && userDict.TryGetValue(activeReviewer.ReviewerId, out var revUser))
        {
            reviewerName = revUser.FullName;
        }

        var evalItems = new List<CouncilMinuteWorkspaceCalculator.ItemEvaluationData>();

        // 1. GVHD
        evalItems.Add(new CouncilMinuteWorkspaceCalculator.ItemEvaluationData(
            ComponentCode: "ADVISOR",
            RoleLabel: "GVHD",
            LecturerName: advisorName,
            Score: advisorEval?.Score,
            Weight: 2m,
            Category: "Nguồn riêng",
            IsDualRoleReviewer: false,
            IsIgnoredInCalculation: advisorEval?.IsIgnoredInCalculation ?? false,
            ExclusionReason: advisorEval?.ExclusionReason));

        // 2. GVPB
        evalItems.Add(new CouncilMinuteWorkspaceCalculator.ItemEvaluationData(
            ComponentCode: "REVIEWER",
            RoleLabel: "GVPB",
            LecturerName: reviewerName,
            Score: activeReviewer?.Evaluation?.Score,
            Weight: 2m,
            Category: "Nguồn riêng",
            IsDualRoleReviewer: isReviewerInCouncil,
            IsIgnoredInCalculation: activeReviewer?.Evaluation?.IsIgnoredInCalculation ?? false,
            ExclusionReason: activeReviewer?.Evaluation?.ExclusionReason));

        // 3. Chủ tịch
        string chairmanFullName = chairmanName ?? "Chủ tịch";
        var chairmanScore = chairman != null && scoreMap.TryGetValue(chairman.Id, out var csChair) ? csChair : null;
        evalItems.Add(new CouncilMinuteWorkspaceCalculator.ItemEvaluationData(
            ComponentCode: "COUNCIL_CHAIRMAN",
            RoleLabel: "Chủ tịch",
            LecturerName: chairmanFullName,
            Score: chairmanScore?.Score,
            Weight: 1m,
            Category: "Thành viên HĐ",
            IsDualRoleReviewer: false,
            IsIgnoredInCalculation: chairmanScore?.IsIgnoredInCalculation ?? false,
            ExclusionReason: chairmanScore?.ExclusionReason));

        // 4. Thư ký
        string secretaryFullName = secretaryName ?? "Thư ký";
        var secretaryScore = secretary != null && scoreMap.TryGetValue(secretary.Id, out var csSec) ? csSec : null;
        evalItems.Add(new CouncilMinuteWorkspaceCalculator.ItemEvaluationData(
            ComponentCode: "COUNCIL_SECRETARY",
            RoleLabel: "Thư ký",
            LecturerName: secretaryFullName,
            Score: secretaryScore?.Score,
            Weight: 1m,
            Category: "Thành viên HĐ",
            IsDualRoleReviewer: false,
            IsIgnoredInCalculation: secretaryScore?.IsIgnoredInCalculation ?? false,
            ExclusionReason: secretaryScore?.ExclusionReason));

        // 5. Ủy viên
        var otherMembers = members
            .Where(m => m.RoleInCouncil == CouncilMemberRole.Member)
            .ToList();

        if (otherMembers.Count > 0)
        {
            foreach (var m in otherMembers)
            {
                var isDualReviewer = activeReviewer != null && m.UserId == activeReviewer.ReviewerId;
                userDict.TryGetValue(m.UserId, out var mUser);
                var mName = mUser?.FullName ?? "Ủy viên";
                var cs = scoreMap.TryGetValue(m.Id, out var s) ? s : null;

                evalItems.Add(new CouncilMinuteWorkspaceCalculator.ItemEvaluationData(
                    ComponentCode: "COUNCIL_MEMBER",
                    RoleLabel: "UV",
                    LecturerName: mName,
                    Score: isDualReviewer ? null : cs?.Score,
                    Weight: isDualReviewer ? 0m : 1m,
                    Category: isDualReviewer ? "Không thiếu điểm" : "Thành viên HĐ",
                    IsDualRoleReviewer: isDualReviewer,
                    IsIgnoredInCalculation: cs?.IsIgnoredInCalculation ?? false,
                    ExclusionReason: cs?.ExclusionReason));
            }
        }
        else if (isReviewerInCouncil)
        {
            // Trường hợp hội đồng không có member role riêng nhưng reviewer có trong hội đồng
            evalItems.Add(new CouncilMinuteWorkspaceCalculator.ItemEvaluationData(
                ComponentCode: "COUNCIL_MEMBER",
                RoleLabel: "UV",
                LecturerName: reviewerName,
                Score: null,
                Weight: 0m,
                Category: "Không thiếu điểm",
                IsDualRoleReviewer: true,
                IsIgnoredInCalculation: false));
        }

        // Xây dựng response bằng calculator
        var (scoreItems, discrepancySummary, calculationPreview) = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(
            evalItems,
            savedSecretaryMarking: null,
            requestedSecretaryMarking: null,
            requestedTargetComponentCode: null,
            overrideReason: minute?.OverrideReason);

        return Result.Success(new CouncilMinuteWorkspaceResponseDto(
            assignmentInfo,
            scoreItems,
            discrepancySummary,
            calculationPreview));
    }
}
