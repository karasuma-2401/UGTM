namespace UGTM.Modules.Notifications.Domain.Entities;

public sealed class NotificationDelivery
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public string Channel { get; set; } = null!;
    public string Destination { get; set; } = null!;
    public string Status { get; set; } = NotificationDeliveryStatuses.Pending;
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }

    public Notification Notification { get; set; } = null!;
}
