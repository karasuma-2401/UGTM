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

public sealed record EmailBatchItem(
    Guid Id,
    EmailMessage Message);

public sealed record EmailBatchItemResult(
    Guid Id,
    bool Succeeded,
    bool IsPermanentFailure = false,
    string? ErrorCode = null,
    string? ErrorMessage = null);
