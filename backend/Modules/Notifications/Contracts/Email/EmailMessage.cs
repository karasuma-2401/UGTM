namespace UGTM.Modules.Notifications.Contracts.Email;

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody = null);