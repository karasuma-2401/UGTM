using UGTM.Modules.ThesisManagement.Application.Common;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.SubmitFinalReport;

public sealed record SubmitFinalReportCommand(
    Guid GroupId,
    string FileName,
    Stream Content,
    IReadOnlyList<SubmissionProductLinkInput>? ProductLinks = null,
    IReadOnlyList<SubmissionAttachmentUpload>? EvidenceImages = null);
