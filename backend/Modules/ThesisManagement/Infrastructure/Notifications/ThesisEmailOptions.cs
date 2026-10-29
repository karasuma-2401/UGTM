namespace UGTM.Modules.ThesisManagement.Infrastructure.Notifications;

public sealed class ThesisEmailOptions
{
    public const string SectionName = "ThesisManagement:Email";

    public Uri PublicWebBaseUrl { get; init; } = new("http://localhost:5173");
}
