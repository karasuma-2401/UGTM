using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.Identity.Application.Authentication.ChangePassword;

public sealed class ChangePasswordHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IAuthenticationSessionRepository sessions,
    IPasswordService passwords,
    IPasswordPolicy passwordPolicy,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock)
{
    public async Task<Result> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated
            || currentUser.UserId is not Guid userId)
        {
            return Result.Failure(IdentityErrors.InvalidSession);
        }

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (string.IsNullOrEmpty(command.CurrentPassword))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    userId,
                    AuditActions.PasswordChanged,
                    AuditOutcomes.Denied,
                    IdentityErrors.CurrentPasswordIncorrect.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.CurrentPasswordIncorrect);
            }

            if (!passwordPolicy.Validate(command.NewPassword).IsValid)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    userId,
                    AuditActions.PasswordChanged,
                    AuditOutcomes.Denied,
                    IdentityErrors.WeakPassword.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.WeakPassword);
            }

            var user = await users.GetByIdAsync(userId, ct);
            if (user?.PasswordHash is null
                || !passwords.Verify(user, user.PasswordHash, command.CurrentPassword))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    userId,
                    AuditActions.PasswordChanged,
                    AuditOutcomes.Denied,
                    IdentityErrors.CurrentPasswordIncorrect.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.CurrentPasswordIncorrect);
            }

            var now = clock.UtcNow;
            user.ChangePassword(passwords.Hash(user, command.NewPassword), now);
            await sessions.RevokeAllAsync(
                user.Id,
                SessionRevocationReason.PasswordChanged,
                now,
                ct);
            auditLogs.Add(AuthenticationAudit.ForUser(
                user.Id,
                AuditActions.PasswordChanged,
                AuditOutcomes.Success));
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }
}
