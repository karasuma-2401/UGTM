namespace UGTM.Modules.Notifications.Infrastructure.Reminders;

public sealed class DeadlineReminderOptions
{
    public const string SectionName = "Notifications:Reminders";

    public bool Enabled { get; set; } = true;
    public int ScanIntervalMinutes { get; set; } = 15;
    public int[] StudentLeadTimeHours { get; set; } = [48, 24];
    public int[] StaffLeadTimeHours { get; set; } = [24];
}

public interface IDeadlineReminderCoordinator
{
    Task<int> RunOnceAsync(CancellationToken cancellationToken = default);
}
