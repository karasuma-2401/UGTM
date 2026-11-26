namespace UGTM.Modules.AcademicPeriods.Contracts.Reminders;

public sealed record OpenBatchStageReminderDto(
    int OrderIndex,
    string StageCode,
    string StageName,
    DateTime DeadlineAtUtc);

public sealed record OpenBatchReminderSnapshotDto(
    Guid BatchId,
    string BatchName,
    string TimeZoneId,
    IReadOnlyList<OpenBatchStageReminderDto> Stages);

public interface IAcademicDeadlineSnapshotReader
{
    Task<IReadOnlyList<OpenBatchReminderSnapshotDto>> GetOpenBatchesAsync(
        CancellationToken cancellationToken = default);
}
