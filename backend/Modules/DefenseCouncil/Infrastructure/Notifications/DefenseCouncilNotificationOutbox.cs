using System.Text.Json;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Notifications;

public sealed class DefenseCouncilNotificationOutbox(DefenseCouncilDbContext db)
    : IDefenseCouncilNotificationOutbox
{
    public const string SourceModuleName = "DefenseCouncil";

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
