namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.GetFinalReportAttachmentDownload;

public sealed record GetFinalReportAttachmentDownloadQuery(Guid AttachmentId, bool Inline = false);
