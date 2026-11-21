using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Contracts.Authorization;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class JwtAccessTokenService(
    JwtOptions options,
    JwtSigningKeyProvider keys) : IAccessTokenService
{
    public AccessTokenResult Create(
        UserProfileDto user,
        Guid sessionId,
        DateTime now)
    {
        var expiresAt = now.AddMinutes(options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("sid", sessionId.ToString())
        };
        if (user.MustChangePassword)
        {
            claims.Add(new Claim(IdentityClaimTypes.PasswordChangeRequired, "true"));
        }
        claims.AddRange(user.Roles.Select(x => new Claim(ClaimTypes.Role, x)));
        claims.AddRange(user.Permissions.Select(x => new Claim("permission", x)));

        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: keys.SigningCredentials);

        return new AccessTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}