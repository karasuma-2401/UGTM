namespace UGTM.Modules.Notifications.Domain.Entities;

public static class NotificationPriorities
{
    public const string Normal = "Normal";
    public const string Critical = "Critical";
}

public static class NotificationChannels
{
    public const string Email = "Email";
    public const string Fcm = "Fcm";
}

public static class NotificationDeliveryStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Accepted = "Accepted";
    public const string RetryScheduled = "RetryScheduled";
    public const string FailedPermanent = "FailedPermanent";
    public const string Skipped = "Skipped";
}

public class Notification
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string NotificationType { get; set; } = null!;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; }

    public string SourceModule { get; set; } = "Notifications";
    public Guid? SourceEventId { get; set; }
    public string? DeduplicationKey { get; set; }
    public string? ResourceType { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid? AcademicBatchId { get; set; }
    public string? ActionPath { get; set; }
    public string Priority { get; set; } = NotificationPriorities.Normal;
    public DateTime? ReadAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public void MarkAsRead(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Read time must be UTC.", nameof(nowUtc));
        }

        IsRead = true;
        ReadAt ??= nowUtc;
    }
}
