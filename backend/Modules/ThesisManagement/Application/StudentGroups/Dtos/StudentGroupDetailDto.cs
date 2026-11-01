namespace UGTM.Modules.ThesisManagement.Application.StudentGroups.Dtos;

public sealed record StudentGroupDetailDto(
    Guid GroupId,
    Guid TopicId,
    string TopicTitleVi,
    string? TopicTitleEn,
    Guid BatchId,
    string? BatchName,
    StudentGroupAdvisorDto Advisor,
    IReadOnlyList<StudentGroupMemberDto> Members,
    string GroupStatus);

public sealed record StudentGroupAdvisorDto(Guid AdvisorId, string? FullName);

public sealed record StudentGroupMemberDto(
    Guid StudentId,
    string? UserCode,
    string? FullName,
    string? Email,
    string ConfirmationStatus);
