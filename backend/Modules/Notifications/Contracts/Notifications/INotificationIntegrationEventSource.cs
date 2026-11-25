namespace UGTM.Modules.Notifications.Contracts.Notifications;

public sealed record NotificationOutboxEntry(
    string NotificationType,
    string Title,
    string Content,
    IReadOnlyList<Guid>? RecipientUserIds = null,
    IReadOnlyList<string>? RecipientRoles = null,
    IReadOnlyList<string>? RecipientPermissions = null,
    IReadOnlyList<Guid>? ExcludeUserIds = null,
    string? DeduplicationKeyPrefix = null,
    string? ResourceType = null,
    Guid? ResourceId = null,
    Guid? AcademicBatchId = null,
    string? ActionPath = null,
    string Priority = "Normal",
    DateTime? ExpiresAtUtc = null,
    bool EnqueuePush = true,
    bool EnqueueEmail = false,
    string? EmailSubject = null,
    string? EmailCategoryLabel = null,
    string? EmailCtaLabel = null,
    string? EmailNotice = null,
    IReadOnlyList<NotificationEmailMetaItem>? EmailMetadata = null,
    IReadOnlyList<NotificationEmailDigestSection>? EmailDigestSections = null);

public sealed record NotificationIntegrationEvent(
    Guid EventId,
    string SourceModule,
    string EventType,
    int PayloadVersion,
    NotificationOutboxEntry Entry,
    DateTime OccurredAtUtc);

public sealed record NotificationOutboxHealth(
    string Source,
    int PendingCount,
    int FailedCount,
    DateTime? OldestPendingUtc);

public static class NotificationIntegrationEventValidation
{
    public static void Validate(NotificationOutboxEntry entry, DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.NotificationType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Content);

        if (entry.NotificationType.Trim().Length > 100)
        {
            throw new ArgumentException("NotificationType cannot exceed 100 characters.", nameof(entry));
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Occurred time must be UTC.", nameof(occurredAtUtc));
        }
    }
}

public interface INotificationIntegrationEventSource
{
    string SourceName { get; }

    Task<NotificationOutboxHealth> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<int> ProcessBatchAsync(
        Func<NotificationIntegrationEvent, CancellationToken, Task> handler,
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default);
}
