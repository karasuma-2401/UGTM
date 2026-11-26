using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.UnlockCouncilMinute;

public sealed class UnlockCouncilMinuteHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    IFinalSubmissionAuthorityReader authorityReader,
    IDefenseCouncilAuditOutbox auditOutbox,
    ICurrentUser currentUser,
    ICouncilMinuteWriteGuard writeGuard,
    TimeProvider? clock = null,
    ICacheService? cache = null,
    IDefenseCouncilNotificationOutbox? notificationOutbox = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public Task<Result<CouncilMinuteLockResponseDto>> HandleAsync(UnlockCouncilMinuteCommand command, CancellationToken ct = default)
        => !currentUser.IsAuthenticated || !currentUser.UserId.HasValue
            ? Task.FromResult(Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.Forbidden))
            : writeGuard.ExecuteAsync(command.AssignmentId, token => HandleCoreAsync(command, token), ct);

    private async Task<Result<CouncilMinuteLockResponseDto>> HandleCoreAsync(
        UnlockCouncilMinuteCommand command,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.Forbidden);
        }

        var reason = command.OverrideReason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10 || reason.Length > 500)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.OverrideReasonRequired);
        }

        var userId = currentUser.UserId.Value;

        var authorityDto = await authorityReader.GetAsync(userId, ct);
        var isAdmin = ScoreSummaryAccess.Role(authorityDto, "Admin");
        var isFaculty = ScoreSummaryAccess.Role(authorityDto, "Faculty");
        var hasUnlockPerm = authorityDto?.Permissions.Contains(PermissionCodes.CouncilMinutesUnlock) == true;

        if (!isAdmin && (!isFaculty || !hasUnlockPerm))
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.UnlockForbidden);
        }

        var assignment = await councilRepository.GetAssignmentWithCouncilAndMembersAsync(command.AssignmentId, ct);
        if (assignment == null)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.AssignmentNotFound);
        }

        var minute = assignment.Minute ?? await councilRepository.GetMinuteByAssignmentIdAsync(assignment.Id, ct);
        if (minute == null || minute.LockStatus != CouncilMinuteLockStatus.Locked)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.MinuteNotLocked);
        }

        var scoreSummary = await councilRepository.GetScoreSummaryByGroupIdAsync(assignment.GroupId, ct);
        if (scoreSummary != null && scoreSummary.Status == ScoreSummaryStatus.PublishedToStudent)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.ScoresAlreadyPublished);
        }

        var previousLockedBy = minute.LockedBy;
        var previousLockedAt = minute.LockedAt;
        var nowUtc = _clock.GetUtcNow().UtcDateTime;

        minute.TryUnlock(userId, reason, nowUtc);

        auditOutbox.Add(new AuditIntegrationEntry(
            TargetType: "CouncilMinute",
            TargetId: minute.Id.ToString(),
            Action: IntegrationAuditActions.CouncilMinuteUnlockGranted,
            Outcome: "Success",
            ReasonCode: "OverrideUnlock",
            Metadata: new
            {
                AssignmentId = assignment.Id,
                CouncilId = assignment.CouncilId,
                GroupId = assignment.GroupId,
                OverrideReason = reason,
                PreviousLockedBy = previousLockedBy,
                PreviousLockedAt = previousLockedAt,
                UnlockedBy = userId,
                UnlockedAt = nowUtc
            }), nowUtc);

        if (notificationOutbox is not null)
        {
            var council = assignment.Council;
            var memberIds = council.Members
                .Select(m => m.UserId)
                .Where(id => id != userId)
                .Distinct()
                .ToArray();

            if (memberIds.Length > 0)
            {
                var shortReason = DefenseCouncilNotificationFormatting.Shorten(reason, 75);
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "COUNCIL_MINUTE_UNLOCKED",
                        Title: $"Đã mở khóa biên bản Hội đồng {council.CouncilCode}",
                        Content: $"Khoa đã mở khóa biên bản Hội đồng {council.CouncilCode} để cập nhật. Lý do: {shortReason}",
                        RecipientUserIds: memberIds,
                        ResourceType: "CouncilMinute",
                        ResourceId: minute.Id,
                        AcademicBatchId: council.BatchId,
                        ActionPath: "/councils/minutes",
                        Priority: "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: false),
                    nowUtc);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        await DefenseCouncilCacheKeys.InvalidateAssignmentAsync(
            cache,
            assignment.Id,
            assignment.CouncilId,
            assignment.Council.BatchId,
            ct);

        return Result.Success(new CouncilMinuteLockResponseDto(
            minute.Id,
            assignment.Id,
            minute.LockStatus.ToString(),
            "Mở khóa đặc biệt",
            minute.LockedAt,
            minute.LockedBy,
            minute.UnlockedAt,
            minute.UnlockedBy,
            minute.OverrideReason));
    }
}
