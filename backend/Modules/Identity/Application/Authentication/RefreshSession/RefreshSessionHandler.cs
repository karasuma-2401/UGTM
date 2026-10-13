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
    IClock clock,
    IdentitySecurityPolicy policy)
{
    public Task<Result<AuthenticatedSessionResult>> HandleAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (!opaqueTokens.TryParse(
                command.RefreshToken,
                out var selector,
                out var validator))
        {
            return Task.FromResult(Result.Failure<AuthenticatedSessionResult>(
                IdentityErrors.InvalidSession));
        }

        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = clock.UtcNow;
            var currentToken = await sessions.GetRefreshTokenForUpdateAsync(selector, ct);
            if (currentToken is null)
            {
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidSession);
            }

            var session = currentToken.Session;
            if (!opaqueTokens.Verify(validator, currentToken.ValidatorHash))
            {
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidSession);
            }

            if (currentToken.ConsumedAt is not null)
            {
                session.Revoke(SessionRevocationReason.RefreshTokenReuse, now);
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<AuthenticatedSessionResult>(
                    IdentityErrors.InvalidSession);
            }

            if (!currentToken.IsUsable(now)
                || !session.CanRefresh(session.User.SecurityStamp, now))
            {
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
