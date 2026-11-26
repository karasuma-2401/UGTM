namespace UGTM.Modules.Notifications.Infrastructure.Delivery;

public sealed class NotificationDeliveryOptions
{
    public const string SectionName = "Notifications:Delivery";

    public bool Enabled { get; set; } = true;
    public string PublicWebBaseUrl { get; set; } = "http://localhost:5173";
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 25;
    public int MaxAttempts { get; set; } = 5;
    public int StaleProcessingMinutes { get; set; } = 10;
    public bool FcmEnabled { get; set; } = false;
}
