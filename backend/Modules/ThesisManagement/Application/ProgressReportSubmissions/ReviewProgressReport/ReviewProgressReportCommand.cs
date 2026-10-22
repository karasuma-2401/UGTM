namespace UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.ReviewProgressReport;

public sealed record ReviewProgressReportCommand(
    Guid MilestoneId,
    string? Decision,
    string? Feedback);
