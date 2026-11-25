namespace UGTM.Modules.Notifications.Domain.Entities;

public sealed class NotificationPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Category { get; set; } = null!;
    public string Channel { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }
}
