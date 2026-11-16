using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.SubmitFinalReport;

public sealed record SubmitFinalReportCommand(
    Guid GroupId,
    string FileName,
    Stream Content,
    IReadOnlyList<SubmissionProductLinkInput>? ProductLinks = null,
    IReadOnlyList<SubmissionAttachmentUpload>? EvidenceImages = null,
    FinalReportVersionType VersionType = FinalReportVersionType.InitialFinal);
