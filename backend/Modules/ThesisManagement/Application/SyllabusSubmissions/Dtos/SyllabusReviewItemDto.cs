using UGTM.Modules.ThesisManagement.Application.StudentGroups.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;

public sealed record SyllabusReviewItemDto(
    Guid SyllabusId,
    Guid GroupId,
    Guid TopicId,
    string TopicTitleVi,
    string? TopicTitleEn,
    Guid BatchId,
    string? BatchName,
    IReadOnlyList<SyllabusReviewStudentDto> Students,
    StudentGroupAdvisorDto Advisor,
    int CurrentVersionNo,
    string CurrentStatus,
    SubmissionFileDto? LatestFile);

public sealed record SyllabusReviewStudentDto(Guid StudentId, string? UserCode, string? FullName);
