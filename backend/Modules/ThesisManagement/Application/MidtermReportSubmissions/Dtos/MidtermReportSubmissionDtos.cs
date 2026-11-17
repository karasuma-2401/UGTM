using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.Dtos;

public sealed record MidtermReportSubmissionDto(
    Guid MidtermReportId,
    Guid GroupId,
    int VersionNo,
    string Status,
    SubmissionFileDto File);

public sealed record MidtermReportSubmissionHistoryItemDto(
    int VersionNo,
    SubmissionFileDto File,
    SubmissionReviewDto? Review);

public sealed record MidtermReportSubmissionHistoryDto(
    Guid MidtermReportId,
    Guid GroupId,
    int CurrentVersionNo,
    string CurrentStatus,
    IReadOnlyList<MidtermReportSubmissionHistoryItemDto> Versions);

public static class MidtermReportSubmissionMapper
{
    public static MidtermReportSubmissionDto ToSubmissionDto(
        MidtermReport midtermReport,
        SubmissionFileDto file)
        => new(
            midtermReport.Id,
            midtermReport.GroupId,
            midtermReport.CurrentVersionNo,
            midtermReport.Status.ToString(),
            file);
}
