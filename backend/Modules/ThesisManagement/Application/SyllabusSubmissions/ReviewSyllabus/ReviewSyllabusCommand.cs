namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.ReviewSyllabus;

public sealed record ReviewSyllabusCommand(
    Guid GroupId,
    string Decision,
    string? Feedback);
