using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Notifications.Application.Notifications.Abstractions;
using UGTM.Modules.Notifications.Application.Notifications.Dtos;
using UGTM.Modules.Notifications.Domain.Entities;

namespace UGTM.Modules.Notifications.Infrastructure.Persistence;

public sealed class NotificationStore(NotificationsDbContext db) : INotificationStore
{
    public async Task<PagedResult<NotificationItemDto>> GetMyNotificationsAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        var query = db.Notifications
            .AsNoTracking()
            .Where(x => x.RecipientId == userId);

        if (unreadOnly)
        {
            query = query.Where(x => !x.IsRead);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NotificationItemDto(
                x.Id,
                x.Title,
                x.Content,
                x.NotificationType,
                x.Priority,
                x.IsRead,
                x.CreatedAt,
                x.ReadAt,
                x.ActionPath,
                x.ResourceType,
                x.ResourceId,
                x.AcademicBatchId,
                x.SourceModule))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<NotificationItemDto>(items, totalCount, pageNumber, pageSize);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
        => db.Notifications
            .AsNoTracking()
            .CountAsync(x => x.RecipientId == userId && !x.IsRead, cancellationToken);

    public async Task<bool> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications
            .SingleOrDefaultAsync(x => x.Id == notificationId && x.RecipientId == userId, cancellationToken);

        if (notification is null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.MarkAsRead(nowUtc);
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var unread = await db.Notifications
            .Where(x => x.RecipientId == userId && !x.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return 0;
        }

        foreach (var item in unread)
        {
            item.MarkAsRead(nowUtc);
        }

        await db.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    public async Task<DeviceRegistrationDto> RegisterDeviceAsync(
        Guid userId,
        string installationId,
        string protectedToken,
        string tokenHash,
        string platform,
        string? userAgent,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var existingByHash = await db.DeviceRegistrations
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        // Revoke any stale active registrations for the same user + installationId with a different token
        var staleSameInstallation = await db.DeviceRegistrations
            .Where(x =>
                x.UserId == userId
                && x.InstallationId == installationId
                && x.RevokedAt == null
                && x.TokenHash != tokenHash)
            .ToListAsync(cancellationToken);

        foreach (var stale in staleSameInstallation)
        {
            stale.Revoke(nowUtc);
        }

        DeviceRegistration registration;
        if (existingByHash is not null)
        {
            existingByHash.ReassignOrRefresh(
                userId,
                installationId,
                protectedToken,
                platform,
                userAgent,
                nowUtc);
            registration = existingByHash;
        }
        else
        {
            registration = new DeviceRegistration
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                InstallationId = installationId,
                Token = protectedToken,
                TokenHash = tokenHash,
                Platform = platform,
                UserAgent = userAgent,
                CreatedAt = nowUtc,
                LastSeenAt = nowUtc,
                RevokedAt = null
            };
            db.DeviceRegistrations.Add(registration);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new DeviceRegistrationDto(
            registration.Id,
            registration.InstallationId,
            registration.Platform,
            registration.LastSeenAt,
            registration.IsActive);
    }

    public async Task<int> RevokeDeviceAsync(
        Guid userId,
        string installationId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var active = await db.DeviceRegistrations
            .Where(x =>
                x.UserId == userId
                && x.InstallationId == installationId
                && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        if (active.Count == 0)
        {
            return 0;
        }

        foreach (var item in active)
        {
            item.Revoke(nowUtc);
        }

        await db.SaveChangesAsync(cancellationToken);
        return active.Count;
    }
}
