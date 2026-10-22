namespace UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.SubmitProgressReport;

public sealed record SubmitProgressReportCommand(
    Guid MilestoneId,
    string? FileName,
    Stream? Content,
    string? GitUrl,
    string? StudentNote);
