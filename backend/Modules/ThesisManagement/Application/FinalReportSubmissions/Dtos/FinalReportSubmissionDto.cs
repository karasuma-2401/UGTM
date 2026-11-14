using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.Dtos;

public sealed record FinalReportSubmissionDto(
    Guid FinalReportId,
    Guid GroupId,
    string VersionType,
    int VersionNo,
    string Status,
    DateTime SubmittedAtUtc,
    SubmissionFileDto File,
    IReadOnlyList<SubmissionProductLinkDto>? ProductLinks = null,
    IReadOnlyList<SubmissionAttachmentDto>? Attachments = null);
