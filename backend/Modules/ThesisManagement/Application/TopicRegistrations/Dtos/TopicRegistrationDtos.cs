namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;

public sealed record TopicRegistrationResultDto(
    Guid GroupId,
    Guid TopicId,
    Guid BatchId,
    bool IsPendingConfirmation,
    DateTime? ConfirmationExpiresAtUtc,
    string Message);

public sealed record ConfirmRegistrationResultDto(
    Guid GroupId,
    Guid TopicId,
    Guid StudentId,
    DateTime ConfirmedAtUtc,
    string Message);

public sealed record StudentRegistrationStatusDto(
    bool HasRegistered,
    Guid? GroupId,
    Guid? TopicId,
    string? TopicTitleVi,
    string? ConfirmationStatus,
    DateTime? ConfirmationExpiresAtUtc,
    int ViolationCount,
    bool IsRegistrationBlocked,
    DateTime? BlockedAtUtc);

public sealed record ExpiredPendingMemberDto(
    Guid GroupId,
    Guid StudentId,
    string? StudentEmail,
    string? StudentFullName,
    Guid TopicId,
    string? TopicTitleVi,
    int NewViolationCount,
    bool IsNowBlocked);

public sealed record AdvisorTopicRegistrationResultDto(
    Guid GroupId,
    Guid TopicId,
    Guid BatchId,
    IReadOnlyList<Guid> AssignedStudentIds,
    string Message);

