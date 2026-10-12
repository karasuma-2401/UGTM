namespace UGTM.Modules.Identity.Application.Security;

public sealed record TokenPair( 
    string AccessToken,
    DateTime AccessTokenExpiresAt, 
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);