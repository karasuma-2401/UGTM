using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;

public sealed record SubmissionFileDto(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSize,
    string FileExtension,
    string ChecksumSha256,
    Guid UploadedBy,
    DateTime UploadedAtUtc);

public sealed record SubmissionReviewDto(
    Guid Id,
    int VersionNo,
    string Decision,
    string? Feedback,
    Guid ReviewedBy,
    DateTime ReviewedAtUtc);

public sealed record SyllabusSubmissionDto(
    Guid SyllabusId,
    Guid GroupId,
    int VersionNo,
    string Status,
    SubmissionFileDto File);

public sealed record SyllabusSubmissionHistoryItemDto(
    int VersionNo,
    SubmissionFileDto File,
    SubmissionReviewDto? Review);

public sealed record SyllabusSubmissionHistoryDto(
    Guid SyllabusId,
    Guid GroupId,
    int CurrentVersionNo,
    string CurrentStatus,
    IReadOnlyList<SyllabusSubmissionHistoryItemDto> Versions);

public sealed record SubmissionFileDownloadDto(
    Guid FileId,
    string DownloadUrl,
    DateTime ExpiresAtUtc);

public static class SyllabusSubmissionMapper
{
    public static SubmissionFileDto ToDto(SubmissionFile file)
        => new(
            file.Id,
            file.FileName,
            file.ContentType,
            file.FileSize,
            file.FileExtension,
            file.ChecksumSha256 ?? string.Empty,
            file.UploadedBy,
            file.UploadedAt);

    public static SubmissionReviewDto ToDto(SubmissionReview review)
        => new(
            review.Id,
            review.VersionNo,
            review.Decision.ToString(),
            review.Feedback,
            review.ReviewedBy,
            review.ReviewedAtUtc);

    public static SyllabusSubmissionDto ToSubmissionDto(Syllabus syllabus, SubmissionFile file)
        => new(
            syllabus.Id,
            syllabus.GroupId,
            syllabus.CurrentVersionNo,
            syllabus.Status.ToString(),
            ToDto(file));
}
