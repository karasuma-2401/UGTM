namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.ReviewMidtermReport;

public sealed record ReviewMidtermReportCommand(Guid GroupId, string Decision, string? Feedback);
