using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.Dtos;

public sealed record FinalReportEvaluationStartedDto(
    Guid FinalReportId,
    Guid GroupId,
    string VersionType,
    int VersionNo,
    string Status,
    Guid StartedBy,
    DateTime StartedAtUtc,
    SubmissionFileDto File);
