namespace UGTM.Modules.Notifications.Application.Notifications.Dtos;

public sealed record NotificationItemDto(
    Guid Id,
    string Title,
    string Content,
    string NotificationType,
    string Priority,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string? ActionPath,
    string? ResourceType,
    Guid? ResourceId,
    Guid? AcademicBatchId,
    string SourceModule);

public sealed record UnreadCountDto(int UnreadCount);

public sealed record MarkAllNotificationsReadDto(int UpdatedCount);

public sealed record DeviceRegistrationDto(
    Guid Id,
    string InstallationId,
    string Platform,
    DateTime LastSeenAt,
    bool IsActive);
