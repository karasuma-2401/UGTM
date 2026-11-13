namespace UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;

public sealed record AcademicResultStudentDto(
    Guid StudentId,
    string StudentCode,
    string FullName,
    string Email);

public sealed record AcademicResultScoreSourceDto(
    string SourceCode,
    string SourceLabel,
    Guid? EvaluatorId,
    string? EvaluatorName,
    decimal? Score,
    decimal? Weight,
    bool IsIgnored,
    string Status,
    string? Comment,
    DateTime? SubmittedAtUtc);

public sealed record AcademicResultMidtermDetailDto(
    string Status,
    string StatusLabel,
    int CurrentVersionNo,
    string? FacultyFeedback,
    Guid? ReviewedBy,
    string? ReviewerName,
    DateTime? ReviewedAtUtc);

public sealed record AcademicResultDetailDto(
    Guid GroupId,
    string GroupCode,
    Guid BatchId,
    string Phase,
    string FacultyName,
    Guid TopicId,
    string? TopicCode,
    string TopicTitleVi,
    string? TopicTitleEn,
    Guid AdvisorId,
    string AdvisorName,
    IReadOnlyList<AcademicResultStudentDto> Students,
    string StatusCode,
    string StatusLabel,
    string DetailLabel,
    AcademicResultMidtermDetailDto? MidtermDetail,
    IReadOnlyList<AcademicResultScoreSourceDto> ScoreSources,
    decimal? AdvisorScore,
    decimal? ReviewerScore,
    decimal? CouncilScore,
    decimal? FinalScore,
    string? FormulaSnapshot,
    string ArchiveStatus,
    string ArchiveStatusLabel,
    string PublicationStatus,
    string PublicationStatusLabel,
    DateTime? PublishedAtUtc);
