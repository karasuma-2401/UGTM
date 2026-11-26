using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilToAcademicAffairs;

public sealed class SubmitCouncilToAcademicAffairsHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    IDefenseGroupContract defenseGroupContract,
    ITopicDefenseContract topicDefenseContract,
    ICouncilUserInfoReader userInfoReader,
    IDefenseCouncilAuditOutbox auditOutbox,
    ICurrentUser currentUser,
    ICouncilSchedulingGuard schedulingGuard,
    ICacheService? cache = null,
    IDefenseCouncilNotificationOutbox? notificationOutbox = null,
    TimeProvider? timeProvider = null)
{
    public async Task<Result<CouncilSubmissionResponseDto>> HandleAsync(
        SubmitCouncilToAcademicAffairsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<CouncilSubmissionResponseDto>(CouncilErrors.Forbidden);
        }

        var isAuthorized = currentUser.Roles.Any(r => r is "Faculty" or "Admin");
        if (!isAuthorized)
        {
            return Result.Failure<CouncilSubmissionResponseDto>(CouncilErrors.Forbidden);
        }

        return await schedulingGuard.ExecuteAsync(token => HandleCoreAsync(command, token), cancellationToken);
    }

    private async Task<Result<CouncilSubmissionResponseDto>> HandleCoreAsync(
        SubmitCouncilToAcademicAffairsCommand command, CancellationToken cancellationToken)
    {
        var council = await councilRepository.GetByIdWithDetailsAsync(command.CouncilId, cancellationToken);
        if (council is null)
        {
            return Result.Failure<CouncilSubmissionResponseDto>(CouncilErrors.NotFound);
        }

        if (council.Status != CouncilStatus.Draft)
        {
            return Result.Failure<CouncilSubmissionResponseDto>(CouncilErrors.NotInDraftStatus);
        }

        var hasChairman = council.Members.Count(m => m.RoleInCouncil == CouncilMemberRole.Chairman) == 1;
        if (!hasChairman)
        {
            return Result.Failure<CouncilSubmissionResponseDto>(CouncilErrors.MissingChairman);
        }

        var hasSecretary = council.Members.Count(m => m.RoleInCouncil == CouncilMemberRole.Secretary) == 1;
        if (!hasSecretary)
        {
            return Result.Failure<CouncilSubmissionResponseDto>(CouncilErrors.MissingSecretary);
        }

        if (council.Assignments.Count == 0)
        {
            return Result.Failure<CouncilSubmissionResponseDto>(CouncilErrors.NoAssignedGroups);
        }

        var roomConflict = await councilRepository.HasRoomScheduleConflictAsync(
            council.DefenseDate,
            council.TimeSlot,
            council.Room,
            council.Id,
            cancellationToken);

        if (roomConflict)
        {
            return Result.Failure<CouncilSubmissionResponseDto>(CouncilErrors.RoomScheduleConflict);
        }

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
            return Result.Failure<CouncilSubmissionResponseDto>(CouncilErrors.MemberScheduleConflict(names));
        }

        var assignedGroupIds = council.Assignments.Select(a => a.GroupId).ToList();

        var rejectedGroupIds = await councilRepository.GetRejectedDefenseGroupIdsAsync(assignedGroupIds, cancellationToken);
        if (rejectedGroupIds.Count > 0)
        {
            return Result.Failure<CouncilSubmissionResponseDto>(
                CouncilErrors.GroupDefenseRejectedByReviewer(rejectedGroupIds.First()));
        }

        var eligibleGroupIds = await councilRepository.LockDefenseEligibleGroupIdsAsync(assignedGroupIds, cancellationToken);
        var ineligibleGroupId = assignedGroupIds.FirstOrDefault(id => !eligibleGroupIds.Contains(id));
        if (ineligibleGroupId != Guid.Empty)
        {
            return Result.Failure<CouncilSubmissionResponseDto>(
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
                return Result.Failure<CouncilSubmissionResponseDto>(CouncilErrors.AdvisorIsInCouncil(advisorName, topic.TitleVi));
            }
        }

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

                return Result.Failure<CouncilSubmissionResponseDto>(
                    CouncilErrors.ReviewerIsChairmanOrSecretaryInCouncil(reviewerName, topicTitle));
            }
        }

        if (!council.Members.Any(member => member.RoleInCouncil == CouncilMemberRole.Member))
            return Result.Failure<CouncilSubmissionResponseDto>(CouncilErrors.MissingMember);
        var nowUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
      
        var actorId = currentUser.UserId!.Value;

        council.SubmitToAcademicAffairs(actorId, command.Notes, nowUtc);

        auditOutbox.Add(
            new AuditIntegrationEntry(
                TargetType: "Council",
                TargetId: council.Id.ToString(),
                Action: "DefenseCouncil.Council.SubmittedToAcademicAffairs",
                Outcome: "Success",
                ReasonCode: null,
                Metadata: new
                {
                    council.CouncilCode,
                    council.DefenseDate,
                    council.TimeSlot,
                    council.Room,
                    SubmittedBy = actorId,
                    SubmittedAt = nowUtc,
                    Notes = command.Notes
                }),
            nowUtc);

        notificationOutbox?.Enqueue(
            new NotificationOutboxEntry(
                NotificationType: "COUNCIL_SUBMITTED_FOR_APPROVAL",
                Title: $"Hội đồng {council.CouncilCode} chờ PĐT phê duyệt",
                Content: $"Khoa đã trình hồ sơ Hội đồng {council.CouncilCode} ({council.Assignments.Count} nhóm) chờ PĐT phê duyệt.",
                RecipientRoles: ["AcademicAffairs"],
                ExcludeUserIds: [actorId],
                ResourceType: "Council",
                ResourceId: council.Id,
                AcademicBatchId: council.BatchId,
                ActionPath: "/councils/approval",
                Priority: "Normal",
                EnqueuePush: true,
                EnqueueEmail: false),
            nowUtc);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await DefenseCouncilCacheKeys.InvalidateCouncilAsync(cache, council.Id, council.BatchId, cancellationToken);

        return Result.Success(new CouncilSubmissionResponseDto(
            council.Id,
            council.CouncilCode,
            council.Status.ToString(),
            council.IsSubmittedToAcademicAffairs,
            council.SubmittedAt,
            council.SubmittedBy,
            council.SubmissionNotes));
    }
}
