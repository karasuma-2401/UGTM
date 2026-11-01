using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.StudentGroups.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.Dtos;

public sealed record MidtermReportReviewItemDto(
    Guid MidtermReportId,
    Guid GroupId,
    Guid TopicId,
    string TopicTitleVi,
    string? TopicTitleEn,
    Guid BatchId,
    string? BatchName,
    IReadOnlyList<MidtermReportReviewStudentDto> Students,
    StudentGroupAdvisorDto Advisor,
    int CurrentVersionNo,
    string CurrentStatus,
    SubmissionFileDto? LatestFile);

public sealed record MidtermReportReviewStudentDto(Guid StudentId, string? UserCode, string? FullName);
