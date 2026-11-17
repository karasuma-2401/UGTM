namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.SubmitSyllabus;

public sealed record SubmitSyllabusCommand(
    Guid GroupId,
    string FileName,
    Stream Content);
