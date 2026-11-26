using System.Text.Json;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.TopicCatalog.Application.Notifications;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Notifications;

public sealed class TopicCatalogNotificationOutbox(TopicCatalogDbContext db)
    : ITopicCatalogNotificationOutbox
{
    public const string SourceModuleName = "TopicCatalog";

    public void Enqueue(NotificationOutboxEntry entry, DateTime occurredAtUtc)
    {
        NotificationIntegrationEventValidation.Validate(entry, occurredAtUtc);

        var eventId = Guid.NewGuid();
        var eventType = entry.NotificationType.Trim();
        var integrationEvent = new NotificationIntegrationEvent(
            eventId,
            SourceModuleName,
            eventType,
            PayloadVersion: 1,
            entry,
            occurredAtUtc);

        var payloadJson = JsonSerializer.Serialize(integrationEvent);
        db.NotificationEventOutboxMessages.Add(NotificationEventOutboxMessage.Create(
            eventId,
            eventType,
            payloadJson,
            occurredAtUtc));
    }
}
