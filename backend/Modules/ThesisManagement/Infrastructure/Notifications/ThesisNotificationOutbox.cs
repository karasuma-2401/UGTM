using System.Text.Json;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Notifications;

public sealed class ThesisNotificationOutbox(ThesisManagementDbContext db)
    : IThesisNotificationOutbox
{
    public const string SourceModuleName = "ThesisManagement";

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
