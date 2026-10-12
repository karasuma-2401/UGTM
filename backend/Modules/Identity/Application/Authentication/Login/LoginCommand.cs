using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Application.Security;

namespace UGTM.Modules.Identity.Application.Authentication.Login;

public sealed record LoginCommand(
    string Identifier,
    string Password,
    string? UserAgentSummary, 
    string? IpAddressHash);
    
public sealed record AuthenticatedSessionResult( 
    TokenPair Tokens,
    UserProfileDto User); 