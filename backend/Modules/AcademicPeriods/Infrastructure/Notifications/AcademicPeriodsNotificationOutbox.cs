using System.Text.Json;
using UGTM.Modules.AcademicPeriods.Application.Notifications;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Notifications;

public sealed class AcademicPeriodsNotificationOutbox(AcademicPeriodsDbContext db)
    : IAcademicPeriodsNotificationOutbox
{
    public const string SourceModuleName = "AcademicPeriods";

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
