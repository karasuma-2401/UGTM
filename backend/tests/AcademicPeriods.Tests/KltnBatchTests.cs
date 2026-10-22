using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Domain.Scheduling;

namespace UGTM.Modules.AcademicPeriods.Tests;

public sealed class KltnBatchTests
{
    [Fact]
    public void Creating_batch_materializes_complete_non_overlapping_schedule()
    {
        var batch = AcademicPeriodTestData.DraftBatch();

        Assert.Equal(BatchStatus.Draft, batch.Status);
        Assert.Equal(7, batch.Stages.Count);
        Assert.Equal(AcademicPeriodTestData.BatchStart, batch.Stages.Min(x => x.StartDate));
        Assert.Equal(AcademicPeriodTestData.BatchEnd, batch.Stages.Max(x => x.EndDate));
        Assert.Equal("TOPIC_PREPARATION", batch.GetStageOn(AcademicPeriodTestData.BatchStart)!.StageCode);
        Assert.Equal("MIDTERM_GATE", batch.GetStageOn(AcademicPeriodTestData.BatchStart.AddDays(63))!.StageCode);
    }

    [Fact]
    public void Schedule_template_converts_local_deadline_to_utc()
    {
        var schedule = AcademicPeriodTestData.ActiveTemplate().Materialize(AcademicPeriodTestData.BatchStart);

        var firstDeadline = schedule[0].DeadlineAtUtc;
        Assert.Equal(DateTimeKind.Utc, firstDeadline.Kind);
        Assert.Equal(new DateTime(2026, 9, 13, 16, 59, 0, DateTimeKind.Utc), firstDeadline);
    }

    [Fact]
    public void Replacing_schedule_rejects_gaps_and_overlaps()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        var invalid = batch.Stages
            .OrderBy(x => x.OrderIndex)
            .Select(x => new StageSchedule(
                x.OrderIndex,
                x.StageCode,
                x.StageName,
                x.StartDate,
                x.EndDate,
                x.DeadlineAtUtc))
            .ToArray();
        invalid[1] = invalid[1] with { StartDate = invalid[1].StartDate.AddDays(1) };

        Assert.Throws<ArgumentException>(() => batch.ReplaceSchedule(
            invalid,
            null,
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc.AddMinutes(1)));
    }

    [Fact]
    public void Batch_lifecycle_is_forward_only()
    {
        var batch = AcademicPeriodTestData.DraftBatch();

        batch.Open(ActiveScoreConfiguration(batch.Id), AcademicPeriodTestData.ActorId, AcademicPeriodTestData.OccurredAtUtc.AddMinutes(1));
        Assert.Equal(BatchStatus.Open, batch.Status);
        Assert.Throws<InvalidOperationException>(() => batch.Archive(
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc.AddMinutes(2)));

        batch.Close(AcademicPeriodTestData.ActorId, AcademicPeriodTestData.OccurredAtUtc.AddMinutes(3));
        batch.Archive(AcademicPeriodTestData.ActorId, AcademicPeriodTestData.OccurredAtUtc.AddMinutes(4));
        Assert.Equal(BatchStatus.Archived, batch.Status);
    }

    [Fact]
    public void Open_batch_schedule_change_requires_reason()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        batch.Open(ActiveScoreConfiguration(batch.Id), AcademicPeriodTestData.ActorId, AcademicPeriodTestData.OccurredAtUtc.AddMinutes(1));
        var schedule = batch.Stages
            .OrderBy(x => x.OrderIndex)
            .Select(x => new StageSchedule(
                x.OrderIndex,
                x.StageCode,
                x.StageName,
                x.StartDate,
                x.EndDate,
                x.DeadlineAtUtc))
            .ToArray();

        Assert.Throws<ArgumentException>(() => batch.ReplaceSchedule(
            schedule,
            null,
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc.AddMinutes(2)));
    }

    private static ScoreWeightConfig ActiveScoreConfiguration(Guid batchId)
    {
        var configuration = ScoreWeightConfig.CreateDraft(
            Guid.NewGuid(),
            batchId,
            1,
            ScoreCalculationMethod.IntegerCoefficient,
            [new ScoreComponentDefinition(
                "COUNCIL",
                "Council",
                ScoreComponentSource.CouncilAverage,
                ScoreComponentApplicability.Always,
                1m,
                true,
                null,
                false,
                1)],
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc);
        configuration.Publish(AcademicPeriodTestData.ActorId, AcademicPeriodTestData.OccurredAtUtc);
        return configuration;
    }
}
