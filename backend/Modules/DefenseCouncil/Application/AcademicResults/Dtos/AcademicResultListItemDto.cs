namespace UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;

public sealed record AcademicResultListItemDto(
    Guid GroupId,
    string GroupCode,
    Guid TopicId,
    string TopicTitleVi,
    string? TopicTitleEn,
    IReadOnlyList<string> StudentNames,
    IReadOnlyList<string> StudentCodes,
    string FacultyName,
    string Phase,
    string StatusCode,
    string StatusLabel,
    string DetailLabel,
    decimal? FinalScore);
