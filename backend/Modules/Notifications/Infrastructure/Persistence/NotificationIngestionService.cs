using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.Notifications.Domain.Entities;

namespace UGTM.Modules.Notifications.Infrastructure.Persistence;

public sealed class NotificationIngestionService(
    NotificationsDbContext db,
    TimeProvider timeProvider) : INotificationIngestionService
{
    public async Task<NotificationIngestionResult> IngestAsync(
        NotificationIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RecipientId == Guid.Empty)
        {
            throw new ArgumentException("RecipientId is required.", nameof(request));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Content);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NotificationType);

        var dedupKey = string.IsNullOrWhiteSpace(request.DeduplicationKey)
            ? null
            : request.DeduplicationKey.Trim();

        if (dedupKey is not null)
        {
            var existingId = await db.Notifications
                .AsNoTracking()
                .Where(x => x.RecipientId == request.RecipientId && x.DeduplicationKey == dedupKey)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingId.HasValue)
            {
                return new NotificationIngestionResult(existingId.Value, Deduplicated: true, DeliveriesEnqueued: 0);
            }
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var priority = string.Equals(request.Priority, NotificationPriorities.Critical, StringComparison.OrdinalIgnoreCase)
            ? NotificationPriorities.Critical
            : NotificationPriorities.Normal;

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientId = request.RecipientId,
            Title = Truncate(request.Title.Trim(), 200),
            Content = request.Content.Trim(),
            NotificationType = Truncate(request.NotificationType.Trim(), 100),
            IsRead = false,
            CreatedAt = nowUtc,
            SourceModule = Truncate(
                string.IsNullOrWhiteSpace(request.SourceModule) ? "Notifications" : request.SourceModule.Trim(),
                64),
            SourceEventId = request.SourceEventId,
            DeduplicationKey = dedupKey is null ? null : Truncate(dedupKey, 200),
            ResourceType = string.IsNullOrWhiteSpace(request.ResourceType) ? null : Truncate(request.ResourceType.Trim(), 64),
            ResourceId = request.ResourceId,
            AcademicBatchId = request.AcademicBatchId,
            ActionPath = string.IsNullOrWhiteSpace(request.ActionPath) ? null : Truncate(request.ActionPath.Trim(), 500),
            Priority = priority,
            ExpiresAt = request.ExpiresAtUtc
        };

        db.Notifications.Add(notification);

        var deliveriesEnqueued = 0;
        var isCritical = priority == NotificationPriorities.Critical;
        var disabledChannels = isCritical
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await db.NotificationPreferences
                .AsNoTracking()
                .Where(x =>
                    x.UserId == request.RecipientId
                    && x.Category == notification.NotificationType
                    && !x.IsEnabled)
                .Select(x => x.Channel)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (request.EnqueueEmail
            && !string.IsNullOrWhiteSpace(request.RecipientEmail)
            && !disabledChannels.Contains(NotificationChannels.Email))
        {
            var emailDestination = request.RecipientEmail.Trim();
            var emailPayload = JsonSerializer.Serialize(new
            {
                subject = string.IsNullOrWhiteSpace(request.EmailSubject)
                    ? notification.Title
                    : request.EmailSubject.Trim(),
                htmlBody = request.EmailHtmlBody,
                textBody = request.EmailTextBody,
                actionPath = notification.ActionPath,
                recipientFullName = request.RecipientFullName,
                categoryLabel = request.EmailCategoryLabel,
                ctaLabel = request.EmailCtaLabel,
                notice = request.EmailNotice,
                metadata = request.EmailMetadata,
                digestSections = request.EmailDigestSections
            });

            db.NotificationDeliveries.Add(new NotificationDelivery
            {
                Id = Guid.NewGuid(),
                NotificationId = notification.Id,
                Channel = NotificationChannels.Email,
                Destination = Truncate(emailDestination, 256),
                Status = NotificationDeliveryStatuses.Pending,
                AttemptCount = 0,
                NextAttemptAt = nowUtc,
                PayloadJson = emailPayload,
                CreatedAt = nowUtc
            });
            deliveriesEnqueued++;
        }

        if (request.EnqueuePush && !disabledChannels.Contains(NotificationChannels.Fcm))
        {
            var activeDeviceIds = await db.DeviceRegistrations
                .AsNoTracking()
                .Where(x => x.UserId == request.RecipientId && x.RevokedAt == null)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (activeDeviceIds.Count > 0)
            {
                var pushPayload = JsonSerializer.Serialize(new
                {
                    title = notification.Title,
                    body = notification.Content,
                    actionPath = notification.ActionPath,
                    notificationType = notification.NotificationType,
                    notificationId = notification.Id
                });

                foreach (var deviceId in activeDeviceIds)
                {
                    db.NotificationDeliveries.Add(new NotificationDelivery
                    {
                        Id = Guid.NewGuid(),
                        NotificationId = notification.Id,
                        Channel = NotificationChannels.Fcm,
                        Destination = deviceId.ToString(),
                        Status = NotificationDeliveryStatuses.Pending,
                        AttemptCount = 0,
                        NextAttemptAt = nowUtc,
                        PayloadJson = pushPayload,
                        CreatedAt = nowUtc
                    });
                    deliveriesEnqueued++;
                }
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new NotificationIngestionResult(notification.Id, Deduplicated: false, deliveriesEnqueued);
        }
        catch (DbUpdateException ex) when (
            dedupKey is not null
            && ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_notification_recipient_deduplication_key"
            })
        {
            db.ChangeTracker.Clear();
            var existingId = await db.Notifications
                .AsNoTracking()
                .Where(x => x.RecipientId == request.RecipientId && x.DeduplicationKey == dedupKey)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return new NotificationIngestionResult(existingId, Deduplicated: true, DeliveriesEnqueued: 0);
        }
    }

    public async Task<IReadOnlyList<NotificationIngestionResult>> IngestBatchAsync(
        IReadOnlyCollection<NotificationIngestionRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var results = new List<NotificationIngestionResult>(requests.Count);
        foreach (var request in requests)
        {
            results.Add(await IngestAsync(request, cancellationToken));
        }

        return results;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
