namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class IdentityClientMetadataOptions
{
    public const string SectionName = "Identity:ClientMetadata";

    public string IpHashKey { get; init; } = string.Empty;
    public int UserAgentMaxLength { get; init; } = 300;
}
