namespace UGTM.Modules.Identity.Infrastructure.Notifications;

public sealed class IdentityEmailOptions
{
    public const string SectionName = "Identity:Email";

    public required Uri PublicWebBaseUrl { get; init; }
}
