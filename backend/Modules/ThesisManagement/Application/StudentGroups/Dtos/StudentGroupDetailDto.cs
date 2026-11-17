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
    string GroupStatus,
    string? Description = null,
    string? Requirements = null,
    int? StudentNumber = null,
    DateTime? CreatedAt = null);

public sealed record StudentGroupAdvisorDto(Guid AdvisorId, string? FullName);

public sealed record StudentGroupMemberDto(
    Guid StudentId,
    string? UserCode,
    string? FullName,
    string? Email,
    string ConfirmationStatus,
    DateTime? JoinedAt = null,
    DateTime? ConfirmedAtUtc = null,
    DateTime? ConfirmationExpiresAtUtc = null,
    Guid? InvitedByStudentId = null,
    bool InvitationExpired = false);
