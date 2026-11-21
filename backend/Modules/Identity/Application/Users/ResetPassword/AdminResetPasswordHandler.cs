using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.Identity.Application.Users.ResetPassword;

public sealed class AdminResetPasswordHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IAuthenticationSessionRepository sessions,
    IOneTimeChallengeRepository challenges,
    IReauthenticationVerifier reauthenticationVerifier,
    IPasswordService passwords,
    IPasswordPolicy passwordPolicy,
    ITemporaryPasswordGenerator temporaryPasswordGenerator,
    IIdentityEmailOutbox emailOutbox,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock)
{
    private const int MaxReasonLength = 500;

    public async Task<Result<AdminResetPasswordResultDto>> HandleAsync(
        AdminResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid actorId || actorId == Guid.Empty)
        {
            return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.InvalidSession);
        }

        if (command.TargetUserId == Guid.Empty)
        {
            return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.InvalidInput);
        }

        if (command.TargetUserId == actorId)
        {
            return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.CannotResetOwnPassword);
        }

        if (string.IsNullOrWhiteSpace(command.Mode))
        {
            return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.InvalidPasswordResetMode);
        }

        var trimmedMode = command.Mode.Trim();
        if (int.TryParse(trimmedMode, out _)
            || !Enum.TryParse<AdminPasswordResetMode>(trimmedMode, ignoreCase: true, out var mode)
            || !Enum.IsDefined(mode))
        {
            return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.InvalidPasswordResetMode);
        }

        if (mode == AdminPasswordResetMode.Custom)
        {
            if (string.IsNullOrEmpty(command.NewPassword) || !passwordPolicy.Validate(command.NewPassword).IsValid)
            {
                return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.WeakPassword);
            }
        }
        else if (!string.IsNullOrEmpty(command.NewPassword))
        {
            return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.InvalidInput);
        }

        var normalizedReason = string.IsNullOrWhiteSpace(command.Reason)
            ? null
            : command.Reason.Trim();
        if (normalizedReason is not null && normalizedReason.Length > MaxReasonLength)
        {
            return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.InvalidInput);
        }

        if (string.IsNullOrEmpty(command.AdminPassword))
        {
            return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.ReauthenticationFailed);
        }

        // Complete re-authentication before opening the write transaction to preserve failure attempt counts and lockout.
        var reauth = await reauthenticationVerifier.VerifyPasswordAsync(
            actorId,
            command.AdminPassword,
            cancellationToken);

        if (reauth == ReauthenticationOutcome.AccountLocked)
        {
            return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.AccountLocked);
        }

        if (reauth != ReauthenticationOutcome.Verified)
        {
            return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.ReauthenticationFailed);
        }

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var target = await users.GetByIdWithRolesAsync(command.TargetUserId, ct);
            if (target is null)
            {
                return Result.Failure<AdminResetPasswordResultDto>(IdentityErrors.UserNotFound);
            }

            var targetIsAdmin = target.UserRoles.Any(ur =>
                string.Equals(ur.Role.RoleName, nameof(SystemRoleName.Admin), StringComparison.OrdinalIgnoreCase));
            var actorIsAdmin = currentUser.Roles.Any(role =>
                string.Equals(role, nameof(SystemRoleName.Admin), StringComparison.OrdinalIgnoreCase));
            if (targetIsAdmin && !actorIsAdmin)
            {
                return await DenyAsync(target.Id, mode, IdentityErrors.CannotManageAdminPassword, ct);
            }

            if (target.Status == AccountStatus.PendingActivation)
            {
                return await DenyAsync(target.Id, mode, IdentityErrors.CannotResetPasswordForPendingAccount, ct);
            }

            var effectivePassword = mode == AdminPasswordResetMode.Custom
                ? command.NewPassword!
                : temporaryPasswordGenerator.Generate();

            var now = clock.UtcNow;
            var passwordHash = passwords.Hash(target, effectivePassword);

            target.SetPasswordByAdmin(passwordHash, command.RequirePasswordChange, now);

            await sessions.RevokeAllAsync(
                target.Id,
                SessionRevocationReason.AdminPasswordReset,
                now,
                ct);
            await challenges.InvalidateOutstandingAsync(
                target.Id,
                ChallengePurpose.PasswordResetOtp,
                now,
                ct);
            await challenges.InvalidateOutstandingAsync(
                target.Id,
                ChallengePurpose.PasswordResetGrant,
                now,
                ct);

            emailOutbox.EnqueueAdminPasswordResetAlertEmail(
                target.Id,
                target.Email,
                target.FullName,
                command.RequirePasswordChange,
                now);

            auditLogs.Add(new AuditEntry(
                TargetType: "User",
                TargetId: target.Id.ToString("D"),
                Action: AuditActions.UserPasswordResetByAdmin,
                Outcome: AuditOutcomes.Success,
                ReasonCode: null,
                Metadata: new
                {
                    Mode = mode.ToString(),
                    RequirePasswordChange = command.RequirePasswordChange,
                    TargetStatus = target.Status.ToString(),
                    Reason = normalizedReason
                }));

            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new AdminResetPasswordResultDto(
                UserId: target.Id,
                Mode: mode.ToString(),
                TemporaryPassword: mode == AdminPasswordResetMode.Generated ? effectivePassword : null,
                MustChangePassword: target.MustChangePassword,
                PasswordChangedAt: now,
                NotificationEmailQueued: true));
        }, cancellationToken);
    }

    private async Task<Result<AdminResetPasswordResultDto>> DenyAsync(
        Guid targetId,
        AdminPasswordResetMode mode,
        Error error,
        CancellationToken ct)
    {
        auditLogs.Add(new AuditEntry(
            TargetType: "User",
            TargetId: targetId.ToString("D"),
            Action: AuditActions.UserPasswordResetByAdmin,
            Outcome: AuditOutcomes.Denied,
            ReasonCode: error.Code,
            Metadata: new { Mode = mode.ToString() }));
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Failure<AdminResetPasswordResultDto>(error);
    }
}
