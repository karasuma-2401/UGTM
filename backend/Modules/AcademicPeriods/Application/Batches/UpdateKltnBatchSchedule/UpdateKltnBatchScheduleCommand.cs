namespace UGTM.Modules.AcademicPeriods.Application.Batches.UpdateKltnBatchSchedule;

public sealed record UpdateStageScheduleItem(
    int OrderIndex,
    string StageCode,
    string StageName,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTimeOffset DeadlineAtUtc);

public sealed record UpdateKltnBatchScheduleCommand(
    Guid BatchId,
    IReadOnlyCollection<UpdateStageScheduleItem> Stages,
    string? ChangeReason);
