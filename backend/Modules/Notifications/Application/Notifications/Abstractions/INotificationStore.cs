using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Notifications.Application.Notifications.Dtos;

namespace UGTM.Modules.Notifications.Application.Notifications.Abstractions;

public interface INotificationStore
{
    Task<PagedResult<NotificationItemDto>> GetMyNotificationsAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<DeviceRegistrationDto> RegisterDeviceAsync(
        Guid userId,
        string installationId,
        string protectedToken,
        string tokenHash,
        string platform,
        string? userAgent,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<int> RevokeDeviceAsync(
        Guid userId,
        string installationId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}

public interface IDeviceTokenProtector
{
    string Protect(string rawToken);
    string Unprotect(string protectedToken);
    string ComputeHash(string rawToken);
}
