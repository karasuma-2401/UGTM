using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Notifications.Domain.Entities;
using UGTM.Modules.Notifications.Infrastructure.Persistence;

namespace UGTM.Modules.Notifications.Infrastructure.Delivery;

public sealed record ClaimedNotificationDelivery(
    Guid DeliveryId,
    Guid NotificationId,
    Guid RecipientId,
    string Channel,
    string Destination,
    int AttemptCount,
    string? PayloadJson,
    string NotificationTitle,
    string NotificationContent,
    string NotificationType,
    string Priority,
    string? ActionPath);

public sealed record NotificationDeliveryOutcome(
    Guid DeliveryId,
    string Status,
    int AttemptCount,
    DateTime NextAttemptAtUtc,
    DateTime? SentAtUtc = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public interface INotificationDeliveryQueueStore
{
    Task<IReadOnlyList<ClaimedNotificationDelivery>> ClaimBatchAsync(
        int batchSize,
        DateTime nowUtc,
        DateTime staleCutoffUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task ApplyOutcomesAsync(
        IReadOnlyCollection<NotificationDeliveryOutcome> outcomes,
        CancellationToken cancellationToken = default);
}

internal sealed class EfNotificationDeliveryQueueStore(NotificationsDbContext db)
    : INotificationDeliveryQueueStore
{
    public async Task<IReadOnlyList<ClaimedNotificationDelivery>> ClaimBatchAsync(
        int batchSize,
        DateTime nowUtc,
        DateTime staleCutoffUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var clampedBatchSize = Math.Clamp(batchSize, 1, 100);
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<IReadOnlyList<ClaimedNotificationDelivery>>(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var deliveries = await db.NotificationDeliveries.FromSqlInterpolated($"""
                SELECT d.*
                FROM notifications.notification_delivery d
                WHERE (d.status IN ('Pending', 'RetryScheduled') AND d.next_attempt_at <= {nowUtc})
                   OR (d.status = 'Processing' AND d.next_attempt_at <= {staleCutoffUtc})
                ORDER BY d.next_attempt_at, d.id
                LIMIT {clampedBatchSize}
                FOR UPDATE SKIP LOCKED
                """).ToListAsync(cancellationToken);

            if (deliveries.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return Array.Empty<ClaimedNotificationDelivery>();
            }

            var notificationIds = deliveries
                .Select(d => d.NotificationId)
                .Distinct()
                .ToArray();

            var notificationsById = await db.Notifications
                .AsNoTracking()
                .Where(n => notificationIds.Contains(n.Id))
                .ToDictionaryAsync(n => n.Id, cancellationToken);

            var leaseExpiresAt = nowUtc.Add(leaseDuration);
            var claimed = new List<ClaimedNotificationDelivery>(deliveries.Count);

            foreach (var delivery in deliveries)
            {
                delivery.Status = NotificationDeliveryStatuses.Processing;
                delivery.NextAttemptAt = leaseExpiresAt;

                if (notificationsById.TryGetValue(delivery.NotificationId, out var notification))
                {
                    claimed.Add(new ClaimedNotificationDelivery(
                        delivery.Id,
                        delivery.NotificationId,
                        notification.RecipientId,
                        delivery.Channel,
                        delivery.Destination,
                        delivery.AttemptCount,
                        delivery.PayloadJson,
                        notification.Title,
                        notification.Content,
                        notification.NotificationType,
                        notification.Priority,
                        notification.ActionPath));
                }
                else
                {
                    delivery.Status = NotificationDeliveryStatuses.FailedPermanent;
                    delivery.LastErrorCode = "MissingNotification";
                    delivery.LastErrorMessage = "Parent notification row was not found.";
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return claimed;
        });
    }

    public async Task ApplyOutcomesAsync(
        IReadOnlyCollection<NotificationDeliveryOutcome> outcomes,
        CancellationToken cancellationToken = default)
    {
        if (outcomes.Count == 0)
        {
            return;
        }

        var ids = outcomes.Select(o => o.DeliveryId).Distinct().ToArray();
        var rows = await db.NotificationDeliveries
            .Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        foreach (var outcome in outcomes)
        {
            if (!rows.TryGetValue(outcome.DeliveryId, out var row))
            {
                continue;
            }

            row.Status = outcome.Status;
            row.AttemptCount = outcome.AttemptCount;
            row.NextAttemptAt = outcome.NextAttemptAtUtc;
            row.SentAt = outcome.SentAtUtc;
            row.LastErrorCode = string.IsNullOrWhiteSpace(outcome.ErrorCode)
                ? null
                : outcome.ErrorCode.Length <= 100
                    ? outcome.ErrorCode
                    : outcome.ErrorCode[..100];
            row.LastErrorMessage = outcome.ErrorMessage;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
