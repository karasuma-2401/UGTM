using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.Dtos;

public sealed record FinalReportHistoryDto(
    Guid FinalReportId,
    Guid GroupId,
    string VersionType,
    int CurrentVersionNo,
    string CurrentStatus,
    DateTime SubmittedAtUtc,
    IReadOnlyList<FinalReportHistoryItemDto> Versions);

public sealed record FinalReportHistoryItemDto(int VersionNo, SubmissionFileDto File);
