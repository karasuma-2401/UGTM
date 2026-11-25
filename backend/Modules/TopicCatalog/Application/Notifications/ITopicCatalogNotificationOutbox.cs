using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.TopicCatalog.Application.Notifications;

public interface ITopicCatalogNotificationOutbox
{
    void Enqueue(NotificationOutboxEntry entry, DateTime occurredAtUtc);
}
