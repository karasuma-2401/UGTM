namespace UGTM.Modules.AcademicPeriods.Domain.Scheduling;

public sealed record StageSchedule(
    int OrderIndex,
    string StageCode,
    string StageName,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime DeadlineAtUtc);
