namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.SubmitFinalReport;

public sealed record SubmitFinalReportCommand(Guid GroupId, string FileName, Stream Content);
