namespace UGTM.Modules.Notifications.Contracts.Email;

public sealed record EmailInlineImage(
    string ContentId,
    string FileName,
    byte[] Content,
    string MediaType = "image/png");

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody = null,
    IReadOnlyCollection<EmailInlineImage>? InlineImages = null);
