using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Application.Authentication.Login;

public sealed class LoginHandler(
    IUserRepository users,
    IAuthenticationSessionRepository sessions,
    IUserProfileReader profiles,
    IPasswordService passwords,
    IOpaqueTokenService opaqueTokens,
    IAccessTokenService accessTokens,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock,
    IdentitySecurityPolicy policy)
{
    public Task<Result<AuthenticatedSessionResult>> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (string.IsNullOrWhiteSpace(command.Identifier)
                || string.IsNullOrEmpty(command.Password))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    null,
                    AuditActions.Login,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidCredentials.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidCredentials);
            }

            var identifier = command.Identifier.Trim().ToUpperInvariant();
            var user = await users.GetByIdentifierAsync(identifier, identifier, ct);
            if (user is null)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    null,
                    AuditActions.Login,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidCredentials.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidCredentials);
            }

            var statusError = user.Status switch
            {
                AccountStatus.PendingActivation => IdentityErrors.AccountPendingActivation,
                AccountStatus.Suspended => IdentityErrors.AccountSuspended,
                AccountStatus.Disabled => IdentityErrors.AccountDisabled,
                _ => Error.None
            };
            if (statusError != Error.None)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    user.Id,
                    AuditActions.Login,
                    AuditOutcomes.Denied,
                    statusError.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidCredentials);
            }

            if (user.PasswordHash is null)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    user.Id,
                    AuditActions.Login,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidCredentials.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidCredentials);
            }

            var now = clock.UtcNow;
            if (user.IsLockedOut(now))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    user.Id,
                    AuditActions.Login,
                    AuditOutcomes.Denied,
                    IdentityErrors.AccountLocked.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.AccountLocked);
            }

            if (!passwords.Verify(user, user.PasswordHash, command.Password))
            {
                var locked = user.RecordFailedLogin(
                    now,
                    policy.MaxFailedLoginAttempts,
                    policy.LockoutDuration);
                auditLogs.Add(AuthenticationAudit.ForUser(
                    user.Id,
                    AuditActions.Login,
                    AuditOutcomes.Denied,
                    locked
                        ? IdentityErrors.AccountLocked.Code
                        : IdentityErrors.InvalidCredentials.Code));
                await unitOfWork.SaveChangesAsync(ct);

                return Result.Failure<AuthenticatedSessionResult>(
                    locked ? IdentityErrors.AccountLocked : IdentityErrors.InvalidCredentials);
            }

            user.RecordSuccessfulLogin(now);
            var session = AuthenticationSession.Create(
                user.Id,
                user.SecurityStamp,
                now,
                policy.SessionIdleLifetime,
                policy.SessionAbsoluteLifetime,
                command.UserAgentSummary,
                command.IpAddressHash);

            var rawRefresh = opaqueTokens.CreateToken();
            var refreshToken = RefreshToken.Create(
                session.Id,
                rawRefresh.Selector,
                rawRefresh.SecretHash,
                now,
                session.IdleExpiresAt);

            sessions.AddSession(session);
            sessions.AddRefreshToken(refreshToken);
            auditLogs.Add(AuthenticationAudit.ForUser(
                user.Id,
                AuditActions.Login,
                AuditOutcomes.Success,
                metadata: new { SessionId = session.Id }));
            await unitOfWork.SaveChangesAsync(ct);

            var profile = await profiles.GetAsync(user.Id, ct)
                          ?? throw new InvalidOperationException("Created user profile cannot be loaded");

            var access = accessTokens.Create(profile, session.Id, now);
            return Result.Success(new AuthenticatedSessionResult(
                new TokenPair(
                    access.Token,
                    access.ExpiresAt,
                    rawRefresh.RawToken,
                    refreshToken.ExpiresAt),
                profile));
        }, cancellationToken);
    }

}
