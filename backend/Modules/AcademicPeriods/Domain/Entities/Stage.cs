using UGTM.Modules.AcademicPeriods.Domain.Scheduling;

namespace UGTM.Modules.AcademicPeriods.Domain.Entities;

public sealed class Stage
{
    private Stage() { }

    private Stage(Guid batchId, StageSchedule schedule, bool isOverridden)
    {
        Id = Guid.NewGuid();
        BatchId = batchId;
        OrderIndex = schedule.OrderIndex;
        StageCode = schedule.StageCode.Trim();
        StageName = schedule.StageName.Trim();
        StartDate = schedule.StartDate;
        EndDate = schedule.EndDate;
        DeadlineAtUtc = schedule.DeadlineAtUtc;
        IsOverridden = isOverridden;
    }

    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public int OrderIndex { get; private set; }
    public string StageCode { get; private set; } = null!;
    public string StageName { get; private set; } = null!;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public DateTime DeadlineAtUtc { get; private set; }
    public bool IsOverridden { get; private set; }

    public KltnBatch Batch { get; private set; } = null!;

    internal static Stage Create(Guid batchId, StageSchedule schedule, string timeZoneId, bool isOverridden)
    {
        if (batchId == Guid.Empty)
        {
            throw new ArgumentException("Batch identifier must be nonempty.", nameof(batchId));
        }

        if (schedule.OrderIndex is < 1 or > KltnBatch.RequiredStageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(schedule), "Stage order must be between 1 and 7.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(schedule.StageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(schedule.StageName);
        if (schedule.StageCode.Trim().Length > 100 || schedule.StageName.Trim().Length > 150)
        {
            throw new ArgumentException("Stage code or name exceeds its maximum length.", nameof(schedule));
        }

        if (schedule.EndDate < schedule.StartDate)
        {
            throw new ArgumentException("Stage end date cannot be before its start date.", nameof(schedule));
        }

        if (schedule.DeadlineAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Stage deadline must be UTC.", nameof(schedule));
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localDeadlineDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(schedule.DeadlineAtUtc, timeZone));
        if (localDeadlineDate < schedule.StartDate || localDeadlineDate > schedule.EndDate)
        {
            throw new ArgumentException("Stage deadline must fall within the stage date range.", nameof(schedule));
        }

        return new Stage(batchId, schedule, isOverridden);
    }
}
