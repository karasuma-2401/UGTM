using UGTM.Modules.Identity.Application.AuditLogs;

namespace UGTM.Modules.Identity.Application.Authentication.Logout;

public sealed class LogoutHandler(
    IAuthenticationSessionRepository sessions,
    IOpaqueTokenService tokens,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
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
            if (token is null)
            {
                return Result.Success();
            }

            if (!tokens.Verify(validator, token.ValidatorHash))
            {
                auditLogs.Add(AuthenticationAudit.ForSession(
                    token.SessionId,
                    AuditActions.SessionLoggedOut,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidSession.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Success();
            }

            token.Session.Revoke(SessionRevocationReason.UserLogout, clock.UtcNow);
            auditLogs.Add(AuthenticationAudit.ForSession(
                token.SessionId,
                AuditActions.SessionLoggedOut,
                AuditOutcomes.Success));
            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success();
        }, cancellationToken);
    }

}
