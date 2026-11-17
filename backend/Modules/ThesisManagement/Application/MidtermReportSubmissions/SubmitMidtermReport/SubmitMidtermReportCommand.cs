namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.SubmitMidtermReport;

public sealed record SubmitMidtermReportCommand(Guid GroupId, string FileName, Stream Content);
