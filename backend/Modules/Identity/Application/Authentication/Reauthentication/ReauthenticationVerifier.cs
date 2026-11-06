using UGTM.BuildingBlocks.Application;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Authentication.Reauthentication;

public sealed class ReauthenticationVerifier(
    IUserRepository users,
    IPasswordService passwords,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock,
    IdentitySecurityPolicy policy) : IReauthenticationVerifier
{
    public async Task<ReauthenticationOutcome> VerifyPasswordAsync(
        Guid userId,
        string password,
        CancellationToken ct = default)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async cancellationToken =>
        {
            if (userId == Guid.Empty || string.IsNullOrEmpty(password))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    userId == Guid.Empty ? null : userId,
                    AuditActions.Reauthentication,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidCredentials.Code));
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ReauthenticationOutcome.InvalidCredentials;
            }

            var user = await users.GetByIdAsync(userId, cancellationToken);
            if (user is null || user.Status != AccountStatus.Active || user.PasswordHash is null)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    userId,
                    AuditActions.Reauthentication,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidCredentials.Code));
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ReauthenticationOutcome.InvalidCredentials;
            }

            var now = clock.UtcNow;
            if (user.IsLockedOut(now))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    user.Id,
                    AuditActions.Reauthentication,
                    AuditOutcomes.Denied,
                    IdentityErrors.AccountLocked.Code));
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ReauthenticationOutcome.AccountLocked;
            }

            if (!passwords.Verify(user, user.PasswordHash, password))
            {
                var locked = user.RecordFailedLogin(
                    now,
                    policy.MaxFailedLoginAttempts,
                    policy.LockoutDuration);

                auditLogs.Add(AuthenticationAudit.ForUser(
                    user.Id,
                    AuditActions.Reauthentication,
                    AuditOutcomes.Denied,
                    locked
                        ? IdentityErrors.AccountLocked.Code
                        : IdentityErrors.InvalidCredentials.Code));
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return locked ? ReauthenticationOutcome.AccountLocked : ReauthenticationOutcome.InvalidCredentials;
            }

            user.RecordSuccessfulLogin(now);
            auditLogs.Add(AuthenticationAudit.ForUser(
                user.Id,
                AuditActions.Reauthentication,
                AuditOutcomes.Success));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ReauthenticationOutcome.Verified;
        }, ct);
    }
}
