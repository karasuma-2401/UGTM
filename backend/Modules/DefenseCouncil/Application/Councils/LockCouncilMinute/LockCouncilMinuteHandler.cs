using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.LockCouncilMinute;

public sealed class LockCouncilMinuteHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    CouncilMinuteWorkspaceLoader workspaceLoader,
    IReauthenticationVerifier reauthenticationVerifier,
    IDefenseCouncilAuditOutbox auditOutbox,
    ICurrentUser currentUser,
    ICouncilMinuteWriteGuard writeGuard,
    IFinalSubmissionAuthorityReader authorityReader,
    TimeProvider? clock = null,
    ICacheService? cache = null,
    IDefenseCouncilNotificationOutbox? notificationOutbox = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<CouncilMinuteLockResponseDto>> HandleAsync(LockCouncilMinuteCommand command, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.Forbidden);
        if (string.IsNullOrWhiteSpace(command.Password))
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.PasswordRequired);
        var authority = await authorityReader.GetAsync(currentUser.UserId.Value, ct);
        if (!ScoreSummaryAccess.Role(authority, "Admin") && !ScoreSummaryAccess.Role(authority, "AcademicAffairs") && !ScoreSummaryAccess.Role(authority, "Lecturer"))
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.OnlySecretaryCanLockMinute);
        var outcome = await reauthenticationVerifier.VerifyPasswordAsync(currentUser.UserId.Value, command.Password, ct);
        if (outcome == ReauthenticationOutcome.AccountLocked)
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.ReauthenticationLocked);
        if (outcome != ReauthenticationOutcome.Verified)
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.ReauthenticationFailed);
        return await writeGuard.ExecuteAsync(command.AssignmentId, token => HandleCoreAsync(command, token), ct);
    }

    private async Task<Result<CouncilMinuteLockResponseDto>> HandleCoreAsync(
        LockCouncilMinuteCommand command,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.PasswordRequired);
        }

        var assignment = await councilRepository.GetAssignmentWithCouncilAndMembersAsync(command.AssignmentId, ct);
        if (assignment == null)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.AssignmentNotFound);
        }

        var council = assignment.Council;
        var members = council.Members.ToList();

        var authority = await authorityReader.GetAsync(currentUser.UserId.Value, ct);
        var isSecretary = ScoreSummaryAccess.Role(authority, "Lecturer") && members.Any(m => m.UserId == currentUser.UserId.Value && m.RoleInCouncil == CouncilMemberRole.Secretary);
        var isAdmin = ScoreSummaryAccess.Role(authority, "Admin") || ScoreSummaryAccess.Role(authority, "AcademicAffairs");

        if (!isSecretary && !isAdmin)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.OnlySecretaryCanLockMinute);
        }

        if (council.Status != CouncilStatus.Active)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.NotActiveForScoring);
        }

        var minute = assignment.Minute ?? await councilRepository.GetMinuteByAssignmentIdAsync(assignment.Id, ct);
        if (minute != null && minute.LockStatus == CouncilMinuteLockStatus.Locked)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.MinuteAlreadyLocked);
        }

        var scoreSummary = await councilRepository.GetScoreSummaryByGroupIdAsync(assignment.GroupId, ct);
        if (scoreSummary != null && scoreSummary.Status == ScoreSummaryStatus.PublishedToStudent)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.ScoresAlreadyPublished);
        }

        var ctx = await workspaceLoader.LoadAsync(assignment, ct, lockConfiguration: true);
        var (_, _, calculationPreview) = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(
            ctx.EvalItems,
            savedSecretaryMarking: null,
            requestedSecretaryMarking: null,
            requestedTargetComponentCode: null,
            overrideReason: minute?.OverrideReason,
            configuration: ctx.Configuration);

        if (!calculationPreview.IsReadyToLock)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.MinuteNotReadyToLock);
        }

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        if (minute == null)
        {
            minute = new CouncilMinute
            {
                Id = Guid.NewGuid(),
                CouncilAssignmentId = assignment.Id,
                LockStatus = CouncilMinuteLockStatus.Open
            };
            await councilRepository.SaveMinuteAsync(minute, ct);
        }

        var previousStatus = minute.LockStatus.ToString();
        minute.TryLock(currentUser.UserId.Value, nowUtc);

        auditOutbox.Add(new AuditIntegrationEntry(
            TargetType: "CouncilMinute",
            TargetId: minute.Id.ToString(),
            Action: IntegrationAuditActions.CouncilMinuteLocked,
            Outcome: "Success",
            ReasonCode: null,
            Metadata: new
            {
                AssignmentId = assignment.Id,
                CouncilId = assignment.CouncilId,
                GroupId = assignment.GroupId,
                PreviousStatus = previousStatus,
                LockedBy = currentUser.UserId.Value,
                LockedAt = nowUtc
            }), nowUtc);

        if (notificationOutbox is not null)
        {
            var otherMemberIds = members
                .Select(m => m.UserId)
                .Where(id => id != currentUser.UserId.Value)
                .Distinct()
                .ToArray();

            if (otherMemberIds.Length > 0)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "COUNCIL_MINUTE_LOCKED",
                        Title: $"Đã khóa biên bản tại Hội đồng {council.CouncilCode}",
                        Content: $"Biên bản bảo vệ tại Hội đồng {council.CouncilCode} đã được khóa và chốt điểm.",
                        RecipientUserIds: otherMemberIds,
                        ResourceType: "CouncilMinute",
                        ResourceId: minute.Id,
                        AcademicBatchId: council.BatchId,
                        ActionPath: "/councils/minutes",
                        Priority: "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: false),
                    nowUtc);
            }

            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: "COUNCIL_MINUTE_LOCKED",
                    Title: $"Hội đồng {council.CouncilCode} đã chốt khóa biên bản",
                    Content: $"Một biên bản bảo vệ thuộc Hội đồng {council.CouncilCode} đã hoàn tất khóa điểm.",
                    RecipientRoles: ["AcademicAffairs"],
                    ExcludeUserIds: [currentUser.UserId.Value],
                    ResourceType: "CouncilMinute",
                    ResourceId: minute.Id,
                    AcademicBatchId: council.BatchId,
                    ActionPath: "/councils/results",
                    Priority: "Normal",
                    EnqueuePush: true,
                    EnqueueEmail: false),
                nowUtc);
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
            "Đã khóa",
            minute.LockedAt,
            minute.LockedBy,
            minute.UnlockedAt,
            minute.UnlockedBy,
            minute.OverrideReason));
    }
}
