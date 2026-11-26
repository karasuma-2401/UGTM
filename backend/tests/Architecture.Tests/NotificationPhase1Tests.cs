using Microsoft.AspNetCore.DataProtection;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Application.Common;
using UGTM.Modules.Notifications.Application.Notifications.Abstractions;
using UGTM.Modules.Notifications.Application.Notifications.Dtos;
using UGTM.Modules.Notifications.Application.Notifications.Handlers;
using UGTM.Modules.Notifications.Domain.Entities;
using UGTM.Modules.Notifications.Infrastructure.Security;

namespace UGTM.Architecture.Tests;

public sealed class NotificationPhase1Tests
{
    private static readonly DateTime NowUtc = new(2026, 11, 25, 8, 30, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void DataProtectionDeviceTokenProtector_protects_unprotects_and_hashes_deterministically()
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = new DataProtectionDeviceTokenProtector(provider);
        const string rawToken = "fcm_token_secret_example_123456789";

        var protectedToken = protector.Protect(rawToken);
        var hash1 = protector.ComputeHash(rawToken);
        var hash2 = protector.ComputeHash(rawToken);

        Assert.NotEqual(rawToken, protectedToken);
        Assert.Equal(rawToken, protector.Unprotect(protectedToken));
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }

    [Fact]
    public void Notification_MarkAsRead_sets_both_IsRead_and_ReadAt_in_UTC()
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientId = UserId,
            Title = "Test",
            Content = "Body",
            NotificationType = "TEST",
            CreatedAt = NowUtc.AddMinutes(-5)
        };

        notification.MarkAsRead(NowUtc);
        Assert.True(notification.IsRead);
        Assert.Equal(NowUtc, notification.ReadAt);

        // Subsequent mark keeps original ReadAt
        notification.MarkAsRead(NowUtc.AddMinutes(10));
        Assert.Equal(NowUtc, notification.ReadAt);
    }

    [Fact]
    public async Task RegisterDeviceHandler_protects_token_computes_hash_and_rejects_invalid_platform()
    {
        var store = new FakeNotificationStore();
        var protector = new DataProtectionDeviceTokenProtector(new EphemeralDataProtectionProvider());
        var handler = new RegisterDeviceHandler(
            store,
            protector,
            new FakeCurrentUser(UserId),
            new FixedTimeProvider(NowUtc));

        var invalidPlatform = await handler.HandleAsync(
            new RegisterDeviceCommand("inst-1", "raw-fcm-token", "windows-phone"));
        Assert.True(invalidPlatform.IsFailure);
        Assert.Equal(NotificationErrors.InvalidRequest.Code, invalidPlatform.Error.Code);

        var success = await handler.HandleAsync(
            new RegisterDeviceCommand("inst-1", "raw-fcm-token", "WEB", "Chrome/130"));
        Assert.True(success.IsSuccess);
        Assert.Equal("inst-1", success.Value.InstallationId);
        Assert.Equal("web", success.Value.Platform);
        Assert.True(success.Value.IsActive);
        Assert.NotEqual("raw-fcm-token", store.LastProtectedToken);
        Assert.Equal(protector.ComputeHash("raw-fcm-token"), store.LastTokenHash);
    }

    [Fact]
    public async Task Inbox_and_device_handlers_require_authentication_and_clamp_pagination()
    {
        var store = new FakeNotificationStore();
        var anonymous = new FakeCurrentUser(null);
        var authenticated = new FakeCurrentUser(UserId);
        var clock = new FixedTimeProvider(NowUtc);

        var unauthList = await new GetMyNotificationsHandler(store, anonymous)
            .HandleAsync(new GetMyNotificationsQuery());
        Assert.True(unauthList.IsFailure);
        Assert.Equal(NotificationErrors.AuthenticationRequired.Code, unauthList.Error.Code);

        var list = await new GetMyNotificationsHandler(store, authenticated)
            .HandleAsync(new GetMyNotificationsQuery(PageNumber: 0, PageSize: 500, UnreadOnly: true));
        Assert.True(list.IsSuccess);
        Assert.Equal(1, store.LastPageNumber);
        Assert.Equal(100, store.LastPageSize);
        Assert.True(store.LastUnreadOnly);

        var unread = await new GetUnreadNotificationCountHandler(store, authenticated).HandleAsync();
        Assert.True(unread.IsSuccess);
        Assert.Equal(3, unread.Value.UnreadCount);

        var markAll = await new MarkAllNotificationsReadHandler(store, authenticated, clock).HandleAsync();
        Assert.True(markAll.IsSuccess);
        Assert.Equal(3, markAll.Value.UpdatedCount);

        var revoke = await new RevokeDeviceHandler(store, authenticated, clock).HandleAsync("inst-1");
        Assert.True(revoke.IsSuccess);
        Assert.Equal("inst-1", store.LastRevokedInstallationId);
    }

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId.HasValue;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakeNotificationStore : INotificationStore
    {
        public int LastPageNumber { get; private set; }
        public int LastPageSize { get; private set; }
        public bool LastUnreadOnly { get; private set; }
        public string? LastProtectedToken { get; private set; }
        public string? LastTokenHash { get; private set; }
        public string? LastRevokedInstallationId { get; private set; }

        public Task<PagedResult<NotificationItemDto>> GetMyNotificationsAsync(
            Guid userId, int pageNumber, int pageSize, bool unreadOnly, CancellationToken cancellationToken = default)
        {
            LastPageNumber = pageNumber;
            LastPageSize = pageSize;
            LastUnreadOnly = unreadOnly;
            return Task.FromResult(new PagedResult<NotificationItemDto>([], 0, pageNumber, pageSize));
        }

        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(3);

        public Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<int> MarkAllAsReadAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(3);

        public Task<DeviceRegistrationDto> RegisterDeviceAsync(
            Guid userId, string installationId, string protectedToken, string tokenHash,
            string platform, string? userAgent, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            LastProtectedToken = protectedToken;
            LastTokenHash = tokenHash;
            return Task.FromResult(new DeviceRegistrationDto(Guid.NewGuid(), installationId, platform, nowUtc, true));
        }

        public Task<int> RevokeDeviceAsync(Guid userId, string installationId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            LastRevokedInstallationId = installationId;
            return Task.FromResult(1);
        }
    }
}
