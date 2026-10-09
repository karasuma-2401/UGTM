
namespace UGTM.Modules.Notifications.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string NotificationType { get; set; } = null!;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; }

}
