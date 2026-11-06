using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.LockCouncilMinute;

public sealed class LockCouncilMinuteHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    CouncilMinuteWorkspaceLoader workspaceLoader,
    IReauthenticationVerifier reauthenticationVerifier,
    IDefenseCouncilAuditOutbox auditOutbox,
    ICurrentUser currentUser,
    TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Result<CouncilMinuteLockResponseDto>> HandleAsync(
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

        // 1. Kiểm tra phân quyền: Chỉ Thư ký của hội đồng hoặc Admin
        var isSecretary = members.Any(m => m.UserId == currentUser.UserId.Value && m.RoleInCouncil == CouncilMemberRole.Secretary);
        var isAdmin = currentUser.Roles.Contains("Admin") || currentUser.Roles.Contains("AcademicAffairs");

        if (!isSecretary && !isAdmin)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.OnlySecretaryCanLockMinute);
        }

        // 2. Kiểm tra trạng thái Hội đồng: phải là Active
        if (council.Status != CouncilStatus.Active)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.NotActiveForScoring);
        }

        // 3. Kiểm tra minute: không được đã ở trạng thái Locked
        var minute = assignment.Minute ?? await councilRepository.GetMinuteByAssignmentIdAsync(assignment.Id, ct);
        if (minute != null && minute.LockStatus == CouncilMinuteLockStatus.Locked)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.MinuteAlreadyLocked);
        }

        // 4. Kiểm tra ScoreSummary: nếu đã PublishedToStudent thì không cho phép thay đổi
        var scoreSummary = await councilRepository.GetScoreSummaryByGroupIdAsync(assignment.GroupId, ct);
        if (scoreSummary != null && scoreSummary.Status == ScoreSummaryStatus.PublishedToStudent)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.ScoresAlreadyPublished);
        }

        // 5. Kiểm tra IsReadyToLock
        var ctx = await workspaceLoader.LoadAsync(assignment, ct);
        var (_, _, calculationPreview) = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(
            ctx.EvalItems,
            savedSecretaryMarking: null,
            requestedSecretaryMarking: null,
            requestedTargetComponentCode: null,
            overrideReason: minute?.OverrideReason);

        if (!calculationPreview.IsReadyToLock)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.MinuteNotReadyToLock);
        }

        // 6. Xác thực mật khẩu Thư ký
        var reauthOutcome = await reauthenticationVerifier.VerifyPasswordAsync(currentUser.UserId.Value, command.Password, ct);
        if (reauthOutcome == ReauthenticationOutcome.AccountLocked)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.ReauthenticationLocked);
        }

        if (reauthOutcome != ReauthenticationOutcome.Verified)
        {
            return Result.Failure<CouncilMinuteLockResponseDto>(CouncilErrors.ReauthenticationFailed);
        }

        // 7. Thực hiện khóa
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

        // 8. Ghi Audit Outbox
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

        await unitOfWork.SaveChangesAsync(ct);

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
