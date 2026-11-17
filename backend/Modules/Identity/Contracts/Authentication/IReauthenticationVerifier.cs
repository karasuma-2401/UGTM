namespace UGTM.Modules.Identity.Contracts.Authentication;

public enum ReauthenticationOutcome
{
    Verified,
    InvalidCredentials,
    AccountLocked,
    Unavailable
}

public interface IReauthenticationVerifier
{
    Task<ReauthenticationOutcome> VerifyPasswordAsync(Guid userId, string password, CancellationToken ct = default);
}
