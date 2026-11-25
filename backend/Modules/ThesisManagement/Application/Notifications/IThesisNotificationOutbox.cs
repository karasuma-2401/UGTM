using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.ThesisManagement.Application.Notifications;

public interface IThesisNotificationOutbox
{
    void Enqueue(NotificationOutboxEntry entry, DateTime occurredAtUtc);
}
