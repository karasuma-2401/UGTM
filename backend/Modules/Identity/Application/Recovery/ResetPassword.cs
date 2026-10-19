using UGTM.Modules.Identity.Application.AuditLogs;

namespace UGTM.Modules.Identity.Application.Recovery;

public sealed record ResetPasswordCommand(string ResetGrant, string NewPassword);

public sealed class ResetPasswordHandler(
    IUserRepository users,
    IOneTimeChallengeRepository challenges,
    IAuthenticationSessionRepository sessions,
    IOpaqueTokenService tokens,
    IPasswordService passwords,
    IPasswordPolicy passwordPolicy,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock)
{
    public Task<Result> HandleAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (!passwordPolicy.Validate(command.NewPassword).IsValid)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    null,
                    AuditActions.PasswordReset,
                    AuditOutcomes.Denied,
                    IdentityErrors.WeakPassword.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.WeakPassword);
            }

            if (!tokens.TryParse(command.ResetGrant, out var selector, out var validator))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    null,
                    AuditActions.PasswordReset,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidResetGrant.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.InvalidResetGrant);
            }

            var now = clock.UtcNow;
            var grant = await challenges.GetForUpdateAsync(
                selector,
                ChallengePurpose.PasswordResetGrant,
                ct);

            if (grant is null
                || !grant.CanAttempt(now)
                || !tokens.Verify(validator, grant.SecretHash))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    grant?.UserId,
                    AuditActions.PasswordReset,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidResetGrant.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.InvalidResetGrant);
            }

            var user = await users.GetByIdAsync(grant.UserId, ct);
            if (user is null || user.Status != AccountStatus.Active)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    grant.UserId,
                    AuditActions.PasswordReset,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidResetGrant.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.InvalidResetGrant);
            }

            user.ChangePassword(passwords.Hash(user, command.NewPassword), now);
            grant.Consume(now);
            await sessions.RevokeAllAsync(
                user.Id,
                SessionRevocationReason.PasswordReset,
                now,
                ct);
            auditLogs.Add(AuthenticationAudit.ForUser(
                user.Id,
                AuditActions.PasswordReset,
                AuditOutcomes.Success));
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }
}
