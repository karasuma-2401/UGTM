namespace UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;

public sealed record AcademicResultExportRowDto(
    string StudentCode,
    string StudentFullName,
    string GroupCode,
    string TopicTitleVi,
    string FacultyName,
    string AdvisorScoreText,
    string ReviewerScoreText,
    string CouncilScoreText,
    string FinalScoreText,
    string ArchiveStatusLabel,
    string PublicationStatusLabel);

public sealed record AcademicResultExportFileDto(
    byte[] FileBytes,
    string ContentType,
    string FileName);
