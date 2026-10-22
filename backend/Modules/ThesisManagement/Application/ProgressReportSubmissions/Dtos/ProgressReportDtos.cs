using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.Dtos;

public sealed record ProgressReportSubmissionDto(
    Guid ProgressReportId,
    Guid MilestoneId,
    Guid GroupId,
    int VersionNo,
    string Status,
    string GitUrl,
    string? StudentNote,
    bool IsLate,
    DateTime SubmittedAtUtc,
    SubmissionFileDto File);

public sealed record ProgressReportReviewDto(
    int VersionNo,
    string Decision,
    string? Feedback,
    Guid ReviewedBy,
    DateTime ReviewedAtUtc);

public sealed record ProgressReportVersionDto(
    int VersionNo,
    string GitUrl,
    string? StudentNote,
    Guid SubmittedBy,
    DateTime SubmittedAtUtc,
    bool IsLate,
    string Status,
    SubmissionFileDto? File,
    ProgressReportReviewDto? Review);

public sealed record ProgressReportHistoryDto(
    Guid ProgressReportId,
    Guid MilestoneId,
    Guid GroupId,
    int CurrentVersionNo,
    string CurrentStatus,
    IReadOnlyList<ProgressReportVersionDto> Versions);
