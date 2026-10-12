namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Identity:Jwt";
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string PrivateKeyPath { get; init; }
    public required string KeyId { get; init; }
    public int AccessTokenMinutes { get; init; } = 10;
}