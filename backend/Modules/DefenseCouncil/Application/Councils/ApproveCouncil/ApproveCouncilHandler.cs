using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.ApproveCouncil;

public sealed record ApproveCouncilCommand(
    Guid CouncilId,
    string DecisionNumber,
    DateOnly? SignedDate = null,
    string? DecisionFileUrl = null);

public sealed class ApproveCouncilHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    IDefenseGroupContract defenseGroupContract,
    ITopicDefenseContract topicDefenseContract,
    ICouncilUserInfoReader userInfoReader,
    IDefenseCouncilAuditOutbox auditOutbox,
    ICurrentUser currentUser,
    ICouncilSchedulingGuard schedulingGuard)
{
    public async Task<Result<CouncilResponseDto>> HandleAsync(
        ApproveCouncilCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.Forbidden);
        }

        var isAcademicAffairsOrAdmin = currentUser.Roles.Any(r => r is "Admin" or "AcademicAffairs");
        if (!isAcademicAffairsOrAdmin)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(command.DecisionNumber))
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.DecisionNumberRequired);
        }

        return await schedulingGuard.ExecuteAsync(token => HandleCoreAsync(command, token), cancellationToken);
    }

    private async Task<Result<CouncilResponseDto>> HandleCoreAsync(
        ApproveCouncilCommand command, CancellationToken cancellationToken)
    {

        var council = await councilRepository.GetByIdWithDetailsAsync(command.CouncilId, cancellationToken);
        if (council is null)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.NotFound);
        }

        if (council.Status != CouncilStatus.Draft)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.AlreadyApproved);
        }

        // Structure check: 1 Chairman, 1 Secretary, Assignments >= 1
        var hasChairman = council.Members.Any(m => m.RoleInCouncil == CouncilMemberRole.Chairman);
        if (!hasChairman)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.MissingChairman);
        }

        var hasSecretary = council.Members.Any(m => m.RoleInCouncil == CouncilMemberRole.Secretary);
        if (!hasSecretary)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.MissingSecretary);
        }

        if (council.Assignments.Count == 0)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.NoAssignedGroups);
        }

        // Re-check room conflict
        var roomConflict = await councilRepository.HasRoomScheduleConflictAsync(
            council.DefenseDate,
            council.TimeSlot,
            council.Room,
            council.Id,
            cancellationToken);

        if (roomConflict)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.RoomScheduleConflict);
        }

        // Re-check member schedule conflict
        var memberUserIds = council.Members.Select(m => m.UserId).Distinct().ToList();
        var conflictingMemberIds = await councilRepository.GetConflictingMemberUserIdsAsync(
            council.DefenseDate,
            council.TimeSlot,
            memberUserIds,
            council.Id,
            cancellationToken);

        if (conflictingMemberIds.Count > 0)
        {
            var usersInfo = await userInfoReader.GetUsersByIdsAsync(conflictingMemberIds, cancellationToken);
            var names = string.Join(", ", conflictingMemberIds.Select(id =>
                usersInfo.TryGetValue(id, out var u) ? u.FullName : id.ToString()));
            return Result.Failure<CouncilResponseDto>(CouncilErrors.MemberScheduleConflict(names));
        }

        // Re-check conflict of interest & defense eligibility
        var assignedGroupIds = council.Assignments.Select(a => a.GroupId).ToList();

        var rejectedGroupIds = await councilRepository.GetRejectedDefenseGroupIdsAsync(assignedGroupIds, cancellationToken);
        if (rejectedGroupIds.Count > 0)
        {
            return Result.Failure<CouncilResponseDto>(
                CouncilErrors.GroupDefenseRejectedByReviewer(rejectedGroupIds.First()));
        }

        var eligibleGroupIds = await councilRepository.LockDefenseEligibleGroupIdsAsync(assignedGroupIds, cancellationToken);
        var ineligibleGroupId = assignedGroupIds.FirstOrDefault(id => !eligibleGroupIds.Contains(id));
        if (ineligibleGroupId != Guid.Empty)
        {
            return Result.Failure<CouncilResponseDto>(
                CouncilErrors.GroupNotEligibleForDefense(ineligibleGroupId));
        }

        var groups = await defenseGroupContract.GetGroupsByIdsAsync(assignedGroupIds, cancellationToken);
        var topicIds = groups.Values.Select(g => g.TopicId).Distinct().ToList();
        var topics = await topicDefenseContract.GetTopicsDetailsAsync(topicIds, cancellationToken);

        var memberSet = memberUserIds.ToHashSet();
        foreach (var topic in topics.Values)
        {
            if (memberSet.Contains(topic.AdvisorId))
            {
                var advisorUser = await userInfoReader.GetUserByIdAsync(topic.AdvisorId, cancellationToken);
                var advisorName = advisorUser?.FullName ?? topic.AdvisorId.ToString();
                return Result.Failure<CouncilResponseDto>(CouncilErrors.AdvisorIsInCouncil(advisorName, topic.TitleVi));
            }
        }

        // Re-check Reviewer role in Council
        var activeReviewers = await councilRepository.GetActiveReviewerIdsByGroupIdsAsync(assignedGroupIds, cancellationToken);
        foreach (var (grpId, revId) in activeReviewers)
        {
            var cMember = council.Members.FirstOrDefault(m => m.UserId == revId);
            if (cMember != null && cMember.RoleInCouncil != CouncilMemberRole.Member)
            {
                var reviewerUser = await userInfoReader.GetUserByIdAsync(revId, cancellationToken);
                var reviewerName = reviewerUser?.FullName ?? revId.ToString();
                var topicTitle = groups.TryGetValue(grpId, out var g) && topics.TryGetValue(g.TopicId, out var t)
                    ? t.TitleVi
                    : grpId.ToString();

                return Result.Failure<CouncilResponseDto>(
                    CouncilErrors.ReviewerIsChairmanOrSecretaryInCouncil(reviewerName, topicTitle));
            }
        }

        if (!council.IsSubmittedToAcademicAffairs)
        {
            return Result.Failure<CouncilResponseDto>(CouncilErrors.NotSubmittedToAcademicAffairs);
        }

        var nowUtc = DateTime.UtcNow;
        var approvedBy = currentUser.UserId ?? Guid.Empty;

        council.Approve(
            command.DecisionNumber,
            command.SignedDate,
            command.DecisionFileUrl,
            approvedBy,
            nowUtc);

        auditOutbox.Add(
            new AuditIntegrationEntry(
                TargetType: "Council",
                TargetId: council.Id.ToString(),
                Action: "DefenseCouncil.Council.Approved",
                Outcome: "Success",
                ReasonCode: null,
                Metadata: new
                {
                    council.CouncilCode,
                    council.DecisionNumber,
                    council.SignedDate,
                    MemberCount = council.Members.Count,
                    GroupCount = council.Assignments.Count
                }),
            nowUtc);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CouncilResponseDto(
            council.Id,
            council.BatchId,
            council.CouncilCode,
            council.DefenseDate,
            council.TimeSlot,
            council.Room,
            council.Status.ToString(),
            council.DecisionNumber,
            council.SignedDate,
            council.DecisionFileUrl,
            council.ApprovedBy,
            council.ApprovedAt));
    }
}
