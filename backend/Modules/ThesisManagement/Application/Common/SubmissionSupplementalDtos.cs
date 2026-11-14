using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public sealed record SubmissionProductLinkInput(
    string? Category,
    string? Label,
    string? Url);

public sealed record SubmissionProductLinkDto(
    Guid Id,
    int DisplayOrder,
    string Category,
    string? Label,
    string Url);

public sealed record SubmissionAttachmentUpload(
    string FileName,
    Stream Content);

public sealed record SubmissionAttachmentDto(
    Guid Id,
    int DisplayOrder,
    string FileName,
    string ContentType,
    long FileSize,
    string FileExtension,
    string ChecksumSha256,
    Guid UploadedBy,
    DateTime UploadedAtUtc);

public static class SubmissionSupplementalMapper
{
    public static SubmissionProductLinkDto ToDto(SubmissionProductLink link)
        => new(
            link.Id,
            link.DisplayOrder,
            link.Category.ToString(),
            link.Label,
            link.Url);

    public static SubmissionAttachmentDto ToDto(SubmissionAttachment attachment)
        => new(
            attachment.Id,
            attachment.DisplayOrder,
            attachment.FileName,
            attachment.ContentType,
            attachment.FileSize,
            attachment.FileExtension,
            attachment.ChecksumSha256 ?? string.Empty,
            attachment.UploadedBy,
            attachment.UploadedAt);
}
