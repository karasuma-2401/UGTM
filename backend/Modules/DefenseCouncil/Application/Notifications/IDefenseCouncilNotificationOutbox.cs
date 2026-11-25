using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.DefenseCouncil.Application.Notifications;

public interface IDefenseCouncilNotificationOutbox
{
    void Enqueue(NotificationOutboxEntry entry, DateTime occurredAtUtc);
}
