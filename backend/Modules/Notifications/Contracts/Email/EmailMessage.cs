namespace UGTM.Modules.Notifications.Contracts.Email;

public sealed record EmailInlineImage(
    string ContentId,
    string FileName,
    byte[] Content);

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody = null,
    IReadOnlyCollection<EmailInlineImage>? InlineImages = null);
