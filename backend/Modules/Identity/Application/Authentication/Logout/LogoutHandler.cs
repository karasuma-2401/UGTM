using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Authentication.Logout;

public sealed class LogoutHandler( 
    IAuthenticationSessionRepository sessions,
    IOpaqueTokenService tokens,
    IIdentityUnitOfWork unitOfWork,
    IClock clock)
{
    public Task<Result> HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken)
            || !tokens.TryParse(command.RefreshToken, out var selector, out var validator))
        {
            return Task.FromResult(Result.Success());
        }

        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var token = await sessions.GetRefreshTokenForUpdateAsync(selector, ct);
            if (token is not null && tokens.Verify(validator, token.ValidatorHash))
            {
                token.Session.Revoke(SessionRevocationReason.UserLogout, clock.UtcNow);
                await unitOfWork.SaveChangesAsync(ct);
            }

            return Result.Success();
        }, cancellationToken);
    }
    
}