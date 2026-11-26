using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.AcademicPeriods.Application.Notifications;

public interface IAcademicPeriodsNotificationOutbox
{
    void Enqueue(NotificationOutboxEntry entry, DateTime occurredAtUtc);
}
