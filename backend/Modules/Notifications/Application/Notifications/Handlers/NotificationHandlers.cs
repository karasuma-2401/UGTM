using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Application.Common;
using UGTM.Modules.Notifications.Application.Notifications.Abstractions;
using UGTM.Modules.Notifications.Application.Notifications.Dtos;

namespace UGTM.Modules.Notifications.Application.Notifications.Handlers;

public sealed record GetMyNotificationsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    bool UnreadOnly = false);

public sealed class GetMyNotificationsHandler(
    INotificationStore store,
    ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<NotificationItemDto>>> HandleAsync(
        GetMyNotificationsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure<PagedResult<NotificationItemDto>>(NotificationErrors.AuthenticationRequired);
        }

        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => query.PageSize
        };

        var result = await store.GetMyNotificationsAsync(
            currentUser.UserId.Value,
            pageNumber,
            pageSize,
            query.UnreadOnly,
            cancellationToken);

        return Result.Success(result);
    }
}

public sealed class GetUnreadNotificationCountHandler(
    INotificationStore store,
    ICurrentUser currentUser)
{
    public async Task<Result<UnreadCountDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure<UnreadCountDto>(NotificationErrors.AuthenticationRequired);
        }

        var count = await store.GetUnreadCountAsync(currentUser.UserId.Value, cancellationToken);
        return Result.Success(new UnreadCountDto(count));
    }
}

public sealed class MarkNotificationReadHandler(
    INotificationStore store,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result> HandleAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure(NotificationErrors.AuthenticationRequired);
        }

        if (notificationId == Guid.Empty)
        {
            return Result.Failure(NotificationErrors.InvalidRequest);
        }

        var updated = await store.MarkAsReadAsync(
            currentUser.UserId.Value,
            notificationId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        return updated
            ? Result.Success()
            : Result.Failure(NotificationErrors.NotFound);
    }
}

public sealed class MarkAllNotificationsReadHandler(
    INotificationStore store,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<MarkAllNotificationsReadDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure<MarkAllNotificationsReadDto>(NotificationErrors.AuthenticationRequired);
        }

        var count = await store.MarkAllAsReadAsync(
            currentUser.UserId.Value,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        return Result.Success(new MarkAllNotificationsReadDto(count));
    }
}

public sealed record RegisterDeviceCommand(
    string InstallationId,
    string Token,
    string? Platform = "web",
    string? UserAgent = null);

public sealed class RegisterDeviceHandler(
    INotificationStore store,
    IDeviceTokenProtector tokenProtector,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly HashSet<string> AllowedPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "web",
        "android",
        "ios"
    };

    public async Task<Result<DeviceRegistrationDto>> HandleAsync(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure<DeviceRegistrationDto>(NotificationErrors.AuthenticationRequired);
        }

        var installationId = command.InstallationId?.Trim() ?? string.Empty;
        var rawToken = command.Token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(installationId)
            || installationId.Length > 128
            || string.IsNullOrWhiteSpace(rawToken)
            || rawToken.Length > 4096)
        {
            return Result.Failure<DeviceRegistrationDto>(NotificationErrors.InvalidRequest);
        }

        var normalizedPlatform = string.IsNullOrWhiteSpace(command.Platform)
            ? "web"
            : command.Platform.Trim().ToLowerInvariant();
        if (!AllowedPlatforms.Contains(normalizedPlatform))
        {
            return Result.Failure<DeviceRegistrationDto>(NotificationErrors.InvalidRequest);
        }

        var userAgent = string.IsNullOrWhiteSpace(command.UserAgent)
            ? null
            : command.UserAgent.Trim();
        if (userAgent is not null && userAgent.Length > 256)
        {
            userAgent = userAgent[..256];
        }

        var protectedToken = tokenProtector.Protect(rawToken);
        var tokenHash = tokenProtector.ComputeHash(rawToken);

        var registration = await store.RegisterDeviceAsync(
            currentUser.UserId.Value,
            installationId,
            protectedToken,
            tokenHash,
            normalizedPlatform,
            userAgent,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        return Result.Success(registration);
    }
}

public sealed class RevokeDeviceHandler(
    INotificationStore store,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result> HandleAsync(string installationId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure(NotificationErrors.AuthenticationRequired);
        }

        var trimmed = installationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 128)
        {
            return Result.Failure(NotificationErrors.InvalidRequest);
        }

        await store.RevokeDeviceAsync(
            currentUser.UserId.Value,
            trimmed,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        return Result.Success();
    }
}
