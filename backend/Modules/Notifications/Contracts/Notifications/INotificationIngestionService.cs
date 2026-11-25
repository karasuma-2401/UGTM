namespace UGTM.Modules.Notifications.Contracts.Notifications;

public sealed record NotificationIngestionRequest(
    Guid RecipientId,
    string Title,
    string Content,
    string NotificationType,
    string SourceModule = "Notifications",
    Guid? SourceEventId = null,
    string? DeduplicationKey = null,
    string? ResourceType = null,
    Guid? ResourceId = null,
    Guid? AcademicBatchId = null,
    string? ActionPath = null,
    string Priority = "Normal",
    DateTime? ExpiresAtUtc = null,
    bool EnqueuePush = true,
    bool EnqueueEmail = false,
    string? RecipientEmail = null,
    string? EmailSubject = null,
    string? EmailHtmlBody = null,
    string? EmailTextBody = null);

public sealed record NotificationIngestionResult(
    Guid? NotificationId,
    bool Deduplicated,
    int DeliveriesEnqueued);

public interface INotificationIngestionService
{
    Task<NotificationIngestionResult> IngestAsync(
        NotificationIngestionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationIngestionResult>> IngestBatchAsync(
        IReadOnlyCollection<NotificationIngestionRequest> requests,
        CancellationToken cancellationToken = default);
}
