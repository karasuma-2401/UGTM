namespace UGTM.Modules.TopicCatalog.Application.Topics.Dtos;

public sealed record TopicStudentDto(
    Guid StudentId,
    string? UserCode,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateTime JoinedAt);

public sealed record TopicGroupDto(
    Guid GroupId,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<TopicStudentDto> Students);

public sealed record TopicSummaryDto(
    Guid Id,
    Guid BatchId,
    Guid AdvisorId,
    string TitleVi,
    string? TitleEn,
    string? Description,
    string? Requirements,
    string Status,
    int? StudentNumber,
    DateTime CreatedAt,
    bool IsPublished = false);

public sealed record TopicTrackingSummaryDto(
    Guid Id,
    Guid BatchId,
    Guid AdvisorId,
    string TitleVi,
    string? TitleEn,
    string? Description,
    string? Requirements,
    string Status,
    int? StudentNumber,
    DateTime CreatedAt,
    int RegisteredStudentsCount,
    int RemainingSlots,
    string RegistrationStatus,
    bool HasRegistration,
    bool IsAvailable,
    TopicGroupDto? Group,
    bool IsPublished = false);

public sealed record TopicTrackingDetailDto(
    Guid Id,
    Guid BatchId,
    Guid AdvisorId,
    string TitleVi,
    string? TitleEn,
    string? Description,
    string? Requirements,
    string Status,
    int? StudentNumber,
    DateTime CreatedAt,
    int RegisteredStudentsCount,
    int RemainingSlots,
    string RegistrationStatus,
    bool HasRegistration,
    bool IsAvailable,
    TopicGroupDto? Group,
    bool IsPublished = false);

public sealed record PublishTopicsResultDto(
    Guid BatchId,
    int PublishedCount,
    IReadOnlyList<Guid> PublishedTopicIds);
