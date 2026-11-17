using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.AcademicPeriods.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class FinalReportSubmissionWindowIntegrationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);
    }

    [Theory]
    [InlineData("Asia/Ho_Chi_Minh", 7)]
    [InlineData("UTC", 0)]
    public async Task Window_boundaries_use_the_batch_timezone_and_include_the_exact_deadline(string timeZone, int offsetHours)
    {
        var batch = await SeedBatchAsync(timeZone: timeZone);
        var opensAt = AcademicPeriodTestData.BatchStart.AddDays(70)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-offsetHours);
        var deadline = AcademicPeriodTestData.BatchStart.AddDays(125)
            .ToDateTime(new TimeOnly(23, 59), DateTimeKind.Utc).AddHours(-offsetHours);
        await using var db = CreateAcademicDbContext();
        var reader = new KltnBatchReader(db);

        foreach (var (instant, expectedOpen) in new[]
        {
            (opensAt.AddTicks(-1), false),
            (opensAt, true),
            (opensAt.AddTicks(1), true),
            (deadline.AddTicks(-1), true),
            (deadline, true),
            (deadline.AddTicks(1), false)
        })
        {
            var window = await reader.GetFinalReportSubmissionWindowAsync(batch.Id, instant);

            Assert.NotNull(window);
            Assert.Equal(batch.Id, window.BatchId);
            Assert.Equal("Open", window.BatchStatus);
            Assert.Equal(expectedOpen, window.IsSubmissionWindowOpen);
            Assert.Equal(opensAt, window.WindowOpensAtUtc);
            Assert.Equal(deadline, window.WindowDeadlineAtUtc);
        }
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Schedule_overrides_control_opening_and_deadline_instead_of_fixed_week_offsets()
    {
        var batch = await SeedBatchAsync(overrideSchedule: true);
        var opensAt = AcademicPeriodTestData.BatchStart.AddDays(73)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
        var stage = batch.Stages.Single(s => s.StageCode == "FINAL_EVALUATION");
        Assert.True(stage.IsOverridden);
        await using var db = CreateAcademicDbContext();
        var reader = new KltnBatchReader(db);

        var originalOpening = await reader.GetFinalReportSubmissionWindowAsync(batch.Id, opensAt.AddDays(-3));
        var opening = await reader.GetFinalReportSubmissionWindowAsync(batch.Id, opensAt);
        var deadline = await reader.GetFinalReportSubmissionWindowAsync(batch.Id, stage.DeadlineAtUtc);
        var afterDeadline = await reader.GetFinalReportSubmissionWindowAsync(batch.Id, stage.DeadlineAtUtc.AddTicks(1));

        Assert.False(originalOpening!.IsSubmissionWindowOpen);
        Assert.True(opening!.IsSubmissionWindowOpen);
        Assert.Equal(opensAt, opening.WindowOpensAtUtc);
        Assert.True(deadline!.IsSubmissionWindowOpen);
        Assert.False(afterDeadline!.IsSubmissionWindowOpen);
        Assert.Equal(stage.DeadlineAtUtc, afterDeadline.WindowDeadlineAtUtc);
    }

    [Theory]
    [InlineData(BatchStatus.Draft)]
    [InlineData(BatchStatus.Closed)]
    [InlineData(BatchStatus.Archived)]
    public async Task Non_open_batches_never_allow_submission(BatchStatus status)
    {
        var batch = await SeedBatchAsync(status);
        var stage = batch.Stages.Single(s => s.StageCode == "FINAL_EVALUATION");
        await using var db = CreateAcademicDbContext();

        var window = await new KltnBatchReader(db).GetFinalReportSubmissionWindowAsync(batch.Id, stage.DeadlineAtUtc);

        Assert.NotNull(window);
        Assert.False(window.IsSubmissionWindowOpen);
        Assert.Equal(status.ToString(), window.BatchStatus);
        Assert.NotNull(window.WindowOpensAtUtc);
    }

    [Fact]
    public async Task An_unconfigured_final_evaluation_stage_does_not_fall_back_to_an_unrelated_stage()
    {
        var batch = await SeedBatchAsync();
        var stage = batch.Stages.Single(s => s.StageCode == "FINAL_EVALUATION");
        await using var db = CreateAcademicDbContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE academic.stage SET stage_code = 'OTHER_STAGE'
            WHERE batch_id = {batch.Id} AND stage_code = 'FINAL_EVALUATION'
            """);

        var window = await new KltnBatchReader(db).GetFinalReportSubmissionWindowAsync(batch.Id, stage.DeadlineAtUtc);

        Assert.NotNull(window);
        Assert.False(window.IsSubmissionWindowOpen);
        Assert.Null(window.WindowOpensAtUtc);
        Assert.Null(window.WindowDeadlineAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown/Timezone")]
    public async Task Invalid_timezone_configuration_cannot_silently_authorize_using_utc(string invalidTimeZone)
    {
        var batch = await SeedBatchAsync();
        var stage = batch.Stages.Single(s => s.StageCode == "FINAL_EVALUATION");
        await using var db = CreateAcademicDbContext();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE academic.kltn_batch SET time_zone_id = {invalidTimeZone} WHERE id = {batch.Id}");

        var window = await new KltnBatchReader(db).GetFinalReportSubmissionWindowAsync(batch.Id, stage.DeadlineAtUtc);

        Assert.NotNull(window);
        Assert.False(window.IsSubmissionWindowOpen);
        Assert.Null(window.WindowOpensAtUtc);
        Assert.Equal(stage.DeadlineAtUtc, window.WindowDeadlineAtUtc);
    }

    [Fact]
    public async Task A_missing_batch_has_no_window()
    {
        await using var db = CreateAcademicDbContext();

        Assert.Null(await new KltnBatchReader(db).GetFinalReportSubmissionWindowAsync(
            Guid.NewGuid(), AcademicPeriodTestData.OccurredAtUtc));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task Window_checks_require_an_explicit_utc_instant(DateTimeKind kind)
    {
        await using var db = CreateAcademicDbContext();

        await Assert.ThrowsAsync<ArgumentException>(() => new KltnBatchReader(db).GetFinalReportSubmissionWindowAsync(
            Guid.NewGuid(), DateTime.SpecifyKind(AcademicPeriodTestData.OccurredAtUtc, kind)));
    }

    private async Task<KltnBatch> SeedBatchAsync(
        BatchStatus status = BatchStatus.Open,
        string timeZone = KltnBatch.DefaultTimeZoneId,
        bool overrideSchedule = false)
    {
        var actor = AcademicPeriodTestData.ActorId;
        var now = AcademicPeriodTestData.OccurredAtUtc;
        var template = ScheduleTemplateVersion.CreateDraft(
            Guid.NewGuid(), "FINAL_WINDOW_TEST", 1, "Final report window test", timeZone,
            AcademicPeriodTestData.DefaultTemplateDefinitions(), actor, now);
        template.Publish(actor, now);
        var batch = KltnBatch.Create(
            Guid.NewGuid(), "Final report test batch", "2026-2027", 1,
            AcademicPeriodTestData.BatchStart, AcademicPeriodTestData.BatchEnd, template, actor, now);
        var scoreConfig = ScoreWeightConfig.CreateDraft(
            Guid.NewGuid(), batch.Id, 1, ScoreCalculationMethod.IntegerCoefficient,
            [new ScoreComponentDefinition("COUNCIL", "Council", ScoreComponentSource.CouncilAverage,
                ScoreComponentApplicability.Always, 1, true, null, false, 1)], actor, now);
        scoreConfig.Publish(actor, now);
        if (status != BatchStatus.Draft) batch.Open(scoreConfig, actor, now);

        if (overrideSchedule)
        {
            var schedule = template.Materialize(batch.StartDate).Select(s => s.OrderIndex switch
            {
                4 => s with { EndDate = s.EndDate.AddDays(3), DeadlineAtUtc = s.DeadlineAtUtc.AddDays(3) },
                5 => s with { StartDate = s.StartDate.AddDays(3), DeadlineAtUtc = s.DeadlineAtUtc.AddDays(-2).AddHours(-6) },
                _ => s
            });
            batch.ReplaceSchedule(schedule, "Adjust final report submission window", actor, now);
        }
        if (status is BatchStatus.Closed or BatchStatus.Archived) batch.Close(actor, now);
        if (status == BatchStatus.Archived) batch.Archive(actor, now);

        await using var db = CreateAcademicDbContext();
        db.ScheduleTemplateVersions.Add(template);
        db.KltnBatches.Add(batch);
        db.ScoreWeightConfigs.Add(scoreConfig);
        await db.SaveChangesAsync();
        return batch;
    }

    private AcademicPeriodsDbContext CreateAcademicDbContext()
    {
        var options = new DbContextOptionsBuilder<AcademicPeriodsDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        return new AcademicPeriodsDbContext(options.Options);
    }

    private UGTMDbContext CreateMigrationDbContext()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString, typeof(UGTMDbContext).Assembly.GetName().Name);
        return new UGTMDbContext(options.Options);
    }

    private static Task ResetSchemasAsync(UGTMDbContext db)
        => db.Database.ExecuteSqlRawAsync(
            """
            DROP SCHEMA IF EXISTS identity CASCADE;
            DROP SCHEMA IF EXISTS academic CASCADE;
            DROP SCHEMA IF EXISTS topics CASCADE;
            DROP SCHEMA IF EXISTS thesis CASCADE;
            DROP SCHEMA IF EXISTS councils CASCADE;
            DROP SCHEMA IF EXISTS notifications CASCADE;
            DROP SCHEMA IF EXISTS public CASCADE;
            CREATE SCHEMA public;
            """);
}
