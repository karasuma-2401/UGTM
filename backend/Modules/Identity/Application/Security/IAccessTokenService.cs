using UGTM.Modules.Identity.Application.Profiles;

namespace UGTM.Modules.Identity.Application.Security;

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);
public interface IAccessTokenService
{
    AccessTokenResult Create(
        UserProfileDto user,
        Guid sessionId,
        DateTime now);
}