using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;

public sealed class SaveCouncilMinuteHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    ICouncilUserInfoReader councilUserInfoReader,
    IDefenseGroupContract defenseGroupContract,
    ITopicDefenseContract topicDefenseContract,
    ICurrentUser currentUser)
{
    private static readonly HashSet<string> ValidComponentCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADVISOR", "GVHD",
        "REVIEWER", "GVPB",
        "COUNCIL_CHAIRMAN", "Chủ tịch", "CHAIRMAN",
        "COUNCIL_SECRETARY", "Thư ký", "SECRETARY",
        "COUNCIL_MEMBER", "UV", "MEMBER"
    };

    public async Task<Result<SaveCouncilMinuteResponseDto>> HandleAsync(
        SaveCouncilMinuteCommand command,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.Forbidden);
        }

        var assignment = await councilRepository.GetAssignmentWithCouncilAndMembersAsync(command.AssignmentId, ct);
        if (assignment == null)
        {
            return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.AssignmentNotFound);
        }

        var council = assignment.Council;
        var members = council.Members.ToList();

        // Kiểm tra phân quyền:
        // Nếu không phải preview, người dùng phải là Thư ký (Secretary) hoặc Admin
        var isSecretary = members.Any(m => m.UserId == currentUser.UserId && m.RoleInCouncil == CouncilMemberRole.Secretary);
        var isAdmin = currentUser.Roles.Contains("Admin") || currentUser.Roles.Contains("AcademicAffairs");

        if (!command.IsPreview && !isSecretary && !isAdmin)
        {
            return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.OnlySecretaryCanSaveMinute);
        }

        // Kiểm tra trạng thái khóa
        var minute = assignment.Minute ?? await councilRepository.GetMinuteByAssignmentIdAsync(assignment.Id, ct);
        if (!command.IsPreview && minute != null && minute.LockStatus == CouncilMinuteLockStatus.Locked)
        {
            return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.MinuteLocked);
        }

        // Validate SecretaryMarking & TargetComponentCode
        var isRejection = string.Equals(command.SecretaryMarking, "RejectedAsPerMinute", StringComparison.OrdinalIgnoreCase);
        if (isRejection)
        {
            if (string.IsNullOrWhiteSpace(command.TargetComponentCode))
            {
                return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.TargetComponentRequiredForRejection);
            }

            if (!ValidComponentCodes.Contains(command.TargetComponentCode.Trim()))
            {
                return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.InvalidTargetComponent(command.TargetComponentCode));
            }
        }

        // Thu thập thông tin User, Topic, Advisor, Reviewer
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

        var advisorEval = await councilRepository.GetAdvisorEvaluationByGroupIdAsync(assignment.GroupId, ct);
        var councilScores = await councilRepository.GetCouncilScoresWithDetailsByAssignmentIdAsync(assignment.Id, ct);
        var scoreMap = councilScores.ToDictionary(s => s.CouncilMemberId);

        var isReviewerInCouncil = activeReviewer != null && members.Any(m => m.UserId == activeReviewer.ReviewerId);

        var chairman = members.FirstOrDefault(m => m.RoleInCouncil == CouncilMemberRole.Chairman);
        var secretary = members.FirstOrDefault(m => m.RoleInCouncil == CouncilMemberRole.Secretary);

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

        string chairmanFullName = "Chủ tịch";
        if (chairman != null && userDict.TryGetValue(chairman.UserId, out var cmUser))
        {
            chairmanFullName = cmUser.FullName;
        }

        string secretaryFullName = "Thư ký";
        if (secretary != null && userDict.TryGetValue(secretary.UserId, out var secUser))
        {
            secretaryFullName = secUser.FullName;
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

        // Tính toán trên memory
        var (scoreItems, discrepancySummary, calculationPreview) = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(
            evalItems,
            savedSecretaryMarking: null,
            requestedSecretaryMarking: command.SecretaryMarking,
            requestedTargetComponentCode: command.TargetComponentCode,
            overrideReason: minute?.OverrideReason);

        if (!command.IsPreview)
        {
            // Thực hiện ghi xuống DB
            // 1. Reset / Cập nhật cờ IsIgnoredInCalculation & ExclusionReason
            var ignoredCode = discrepancySummary.IgnoredComponentCode;

            // Update Advisor
            if (advisorEval != null)
            {
                var isIgnored = string.Equals(ignoredCode, "ADVISOR", StringComparison.OrdinalIgnoreCase);
                advisorEval.SetCalculationExclusion(isIgnored, isIgnored ? "RejectedAsPerMinute" : null);
            }

            // Update Reviewer
            if (activeReviewer?.Evaluation != null)
            {
                var isIgnored = string.Equals(ignoredCode, "REVIEWER", StringComparison.OrdinalIgnoreCase);
                activeReviewer.Evaluation.SetCalculationExclusion(isIgnored, isIgnored ? "RejectedAsPerMinute" : null);
            }

            // Update CouncilScores
            foreach (var cs in councilScores)
            {
                var member = members.FirstOrDefault(m => m.Id == cs.CouncilMemberId);
                var code = member?.RoleInCouncil switch
                {
                    CouncilMemberRole.Chairman => "COUNCIL_CHAIRMAN",
                    CouncilMemberRole.Secretary => "COUNCIL_SECRETARY",
                    _ => "COUNCIL_MEMBER"
                };

                var isIgnored = string.Equals(ignoredCode, code, StringComparison.OrdinalIgnoreCase);
                cs.IsIgnoredInCalculation = isIgnored;
                cs.ExclusionReason = isIgnored ? "RejectedAsPerMinute" : null;
            }

            // 2. Lưu CouncilMinute
            var overrideMarker = string.Equals(command.SecretaryMarking, "AcceptedAsPerMinute", StringComparison.OrdinalIgnoreCase)
                ? "ACCEPTED_AS_PER_MINUTE"
                : null;

            await councilRepository.SaveCouncilMinuteDraftAsync(
                assignment.Id,
                command.StudentFeedback,
                overrideMarker,
                ct);

            await unitOfWork.SaveChangesAsync(ct);
        }

        var currentLockStatus = minute?.LockStatus.ToString() ?? "Open";

        return Result.Success(new SaveCouncilMinuteResponseDto(
            assignment.Id,
            currentLockStatus,
            command.StudentFeedback ?? minute?.RequiredRevisions ?? minute?.GeneralFeedback,
            command.IsPreview,
            discrepancySummary,
            calculationPreview,
            scoreItems));
    }
}
