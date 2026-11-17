using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    private async Task WaitForScoreSqlLockAsync(string pattern)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var db = MigrationDb();
        while (!await db.Database.SqlQuery<bool>($"SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE pid<>pg_backend_pid() AND wait_event_type='Lock' AND query LIKE {"%" + pattern + "%"}) AS \"Value\"").SingleAsync(timeout.Token))
            await Task.Delay(30, timeout.Token);
    }
    [Fact]
    public async Task ScoreSummary_config_retired_while_waiting_is_reselected_from_a_fresh_active_statement()
    {
        await PrepareScoreCalculationAsync();
        await using var publisher = Db<AcademicPeriodsDbContext>();
        await using var tx = await publisher.Database.BeginTransactionAsync();
        var old = await publisher.ScoreWeightConfigs.Include(c => c.Components).SingleAsync(c => c.Status == ConfigurationVersionStatus.Active);
        await publisher.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM academic.score_weight_config WHERE id={old.Id} FOR UPDATE");
        var calculating = CalculateAsync(); Guid nextId = Guid.Empty;
        try
        {
            await WaitForScoreSqlLockAsync("score_weight_config");
            var definitions = old.Components.Select(c => new ScoreComponentDefinition(c.ComponentCode, c.ComponentName, c.Source, c.Applicability, c.Weight, c.IsRequired, c.MinimumScore, c.RequiresApproval, c.OrderIndex)).ToArray();
            old.Retire(_clock.UtcNow); await publisher.SaveChangesAsync();
            var next = ScoreWeightConfig.CreateDraft(Guid.NewGuid(), _batchId, old.VersionNo + 1, old.CalculationMethod, definitions, _adminId, _clock.UtcNow, "Race publication");
            next.Publish(_adminId, _clock.UtcNow); nextId = next.Id; publisher.ScoreWeightConfigs.Add(next); await publisher.SaveChangesAsync();
        }
        finally { await tx.CommitAsync(); }
        using var response = await calculating; var result = await ArchivalDataAsync(response); Assert.Equal(3, result.GetProperty("configVersionNo").GetInt32());
        await using var check = Db<DefenseCouncilDbContext>(); Assert.Equal(nextId, (await check.ScoreSummaryRevisions.SingleAsync()).ConfigId);
    }
    [Fact]
    public async Task ScoreSummary_sources_are_locked_until_receipt_commit_and_later_source_change_is_stale()
    {
        await PrepareScoreCalculationAsync();
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION councils.wait_score_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(710103); RETURN NEW; END $$;
            CREATE TRIGGER wait_score_receipt BEFORE INSERT ON councils.score_summary_idempotency FOR EACH ROW EXECUTE FUNCTION councils.wait_score_receipt();
            """);
        await using var blocker = MigrationDb(); await using var gate = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(710103)");
        var calculation = CalculateAsync(); Task? changing = null;
        async Task ChangeMinuteAsync()
        { await using var db = MigrationDb(); await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_minute SET general_feedback='changed after calculation'"); }
        try
        {
            await WaitForScoreSqlLockAsync("score_summary_idempotency");
            changing = ChangeMinuteAsync(); await WaitForScoreSqlLockAsync("UPDATE councils.council_minute"); Assert.False(changing.IsCompleted);
        }
        finally { await gate.CommitAsync(); }
        using var response = await calculation; await ArchivalDataAsync(response); if (changing is not null) await changing;
        using var read = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/group/{_groupId}", _adminId, "Admin"); Assert.True((await ArchivalDataAsync(read)).GetProperty("isStale").GetBoolean());
    }
    [Fact]
    public async Task ScoreSummary_shared_coordinator_rejects_reader_mutation_and_rolls_back_writer()
    {
        await PrepareScoreCalculationAsync(); using var scope = _app.Services.CreateScope(); var services = scope.ServiceProvider;
        var coordinator = services.GetRequiredService<IScoreSummaryWorkflowTransactionCoordinator>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteWithPostSaveAsync(async token =>
        {
            DbContext[] contexts = [services.GetRequiredService<DefenseCouncilDbContext>(), services.GetRequiredService<ThesisManagementDbContext>(), services.GetRequiredService<IdentityDbContext>(), services.GetRequiredService<AcademicPeriodsDbContext>(), services.GetRequiredService<UGTM.Modules.TopicCatalog.Infrastructure.Persistence.TopicCatalogDbContext>()];
            var backends = new List<int>();
            foreach (var context in contexts) backends.Add(await context.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
            Assert.Single(backends.Distinct());
            var db = services.GetRequiredService<ThesisManagementDbContext>();
            var submission = await db.FinalSubmissions.SingleAsync(token); submission.FacultyFeedback = "forbidden reader change";
            services.GetRequiredService<DefenseCouncilDbContext>().ScoreSummarys.Add(new UGTM.Modules.DefenseCouncil.Domain.Entities.ScoreSummary { Id = Guid.NewGuid(), GroupId = _groupId });
            return ScoreSummaryTransactionResult<bool>.Commit(true);
        }, (value, _) => Task.FromResult(value)));
        await using var check = Db<ThesisManagementDbContext>(); Assert.Null((await check.FinalSubmissions.SingleAsync()).FacultyFeedback);
        await using var defense = Db<DefenseCouncilDbContext>(); Assert.Empty(await defense.ScoreSummarys.ToListAsync());
    }
    [Fact]
    public async Task ScoreSummary_council_score_API_updates_progress_without_creating_or_overwriting_summary()
    {
        await PrepareScoreCalculationAsync();
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_minute SET lock_status='Open'");
        using var scored = await SendAsync(HttpMethod.Post, $"/api/councils/assignments/{_scoreAssignmentId}/scores", _adminId, "Admin", JsonContent.Create(new { details = new[] { new { criteriaId = _scoreCriteriaId, score = 9 } }, feedback = "source score only" }));
        await ArchivalDataAsync(scored);
        await using var defense = Db<DefenseCouncilDbContext>(); Assert.Empty(await defense.ScoreSummarys.ToListAsync());
        Assert.Equal(UGTM.Modules.DefenseCouncil.Domain.Enums.CouncilMinuteLockStatus.AllScoresEntered, (await defense.CouncilMinutes.SingleAsync()).LockStatus);
        using var calculation = await CalculateAsync(); Assert.Equal(HttpStatusCode.Conflict, calculation.StatusCode);
    }
}
