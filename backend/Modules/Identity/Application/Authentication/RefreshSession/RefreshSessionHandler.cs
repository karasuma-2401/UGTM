using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Authentication.Login;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Application.Authentication.RefreshSession;

public class RefreshSessionHandler(
    IAuthenticationSessionRepository sessions,
    IUserProfileReader profiles,
    IOpaqueTokenService opaqueTokens,
    IAccessTokenService accessTokens,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock,
    IdentitySecurityPolicy policy)
{
    public Task<Result<AuthenticatedSessionResult>> HandleAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (!opaqueTokens.TryParse(
                    command.RefreshToken,
                    out var selector,
                    out var validator))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    null,
                    AuditActions.SessionRefreshed,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidSession.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidSession);
            }

            var now = clock.UtcNow;
            var currentToken = await sessions.GetRefreshTokenForUpdateAsync(selector, ct);
            if (currentToken is null)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    null,
                    AuditActions.SessionRefreshed,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidSession.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidSession);
            }

            var session = currentToken.Session;
            if (!opaqueTokens.Verify(validator, currentToken.ValidatorHash))
            {
                auditLogs.Add(AuthenticationAudit.ForSession(
                    session.Id,
                    AuditActions.SessionRefreshed,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidSession.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidSession);
            }

            if (currentToken.ConsumedAt is not null)
            {
                session.Revoke(SessionRevocationReason.RefreshTokenReuse, now);
                auditLogs.Add(AuthenticationAudit.ForSession(
                    session.Id,
                    AuditActions.RefreshTokenReuseDetected,
                    AuditOutcomes.Denied,
                    SessionRevocationReason.RefreshTokenReuse.ToString()));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidSession);
            }

            if (!currentToken.IsUsable(now)
                || !session.CanRefresh(session.User.SecurityStamp, now))
            {
                auditLogs.Add(AuthenticationAudit.ForSession(
                    session.Id,
                    AuditActions.SessionRefreshed,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidSession.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidSession);
            }

            session.Touch(now, policy.SessionIdleLifetime);
            var rawReplacement = opaqueTokens.CreateToken();
            var replacement = RefreshToken.Create(
                session.Id,
                rawReplacement.Selector,
                rawReplacement.SecretHash,
                now,
                session.IdleExpiresAt);

            currentToken.Consume(replacement.Id, now);
            sessions.AddRefreshToken(replacement);
            auditLogs.Add(AuthenticationAudit.ForSession(
                session.Id,
                AuditActions.SessionRefreshed,
                AuditOutcomes.Success));
            await unitOfWork.SaveChangesAsync(ct);

            var profile = await profiles.GetAsync(session.UserId, ct)
                ?? throw new InvalidOperationException("Session user profile cannot be loaded.");
            var access = accessTokens.Create(profile, session.Id, now);

            return Result.Success(new AuthenticatedSessionResult(
                new TokenPair(
                    access.Token,
                    access.ExpiresAt,
                    rawReplacement.RawToken,
                    replacement.ExpiresAt),
                profile));
        }, cancellationToken);
    }
}
