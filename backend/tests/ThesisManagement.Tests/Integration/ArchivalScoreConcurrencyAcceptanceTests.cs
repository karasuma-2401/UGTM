using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    [Fact]
    public async Task ArchivalScore_publication_detects_new_configuration_when_publisher_wins_lock()
    {
        await PrepareScoreCalculationAsync(); using var calculation = await CalculateAsync(); var calculated = await ArchivalDataAsync(calculation);
        var body = new { batchId = _batchId, selections = new[] { new { groupId = _groupId, expectedSummaryVersion = calculated.GetProperty("summaryVersion").GetUInt32() } } };
        await using var publisher = Db<AcademicPeriodsDbContext>(); await using var transaction = await publisher.Database.BeginTransactionAsync();
        var old = await publisher.ScoreWeightConfigs.Include(c => c.Components).SingleAsync(c => c.Status == ConfigurationVersionStatus.Active);
        await publisher.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM academic.score_weight_config WHERE id={old.Id} FOR UPDATE");
        var publication = PublishScoresAsync(body);
        try
        {
            await WaitForScoreSqlLockAsync("score_weight_config");
            var definitions = old.Components.Select(c => new ScoreComponentDefinition(c.ComponentCode, c.ComponentName, c.Source, c.Applicability, c.Weight, c.IsRequired, c.MinimumScore, c.RequiresApproval, c.OrderIndex)).ToArray();
            old.Retire(_clock.UtcNow); await publisher.SaveChangesAsync();
            var next = ScoreWeightConfig.CreateDraft(Guid.NewGuid(), _batchId, old.VersionNo + 1, old.CalculationMethod, definitions, _adminId, _clock.UtcNow, "Configuration race");
            next.Publish(_adminId, _clock.UtcNow); publisher.ScoreWeightConfigs.Add(next); await publisher.SaveChangesAsync();
        }
        finally { await transaction.CommitAsync(); }
        using var response = await publication; Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var verify = Db<DefenseCouncilDbContext>(); Assert.Equal(ScoreSummaryStatus.Calculated, (await verify.ScoreSummarys.SingleAsync()).Status);
        Assert.Equal(0, await verify.ScoreSummaryIdempotencyRecords.CountAsync(r => r.Operation == "ScoreSummary.Publish"));
    }

    [Fact]
    public async Task ArchivalScore_configuration_waits_for_publication_then_student_keeps_published_snapshot()
    {
        await PrepareScoreCalculationAsync(); using var calculation = await CalculateAsync(); var calculated = await ArchivalDataAsync(calculation);
        var body = new { batchId = _batchId, selections = new[] { new { groupId = _groupId, expectedSummaryVersion = calculated.GetProperty("summaryVersion").GetUInt32() } } };
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION councils.wait_acceptance_publish() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(710106); RETURN NEW; END $$;
            CREATE TRIGGER wait_acceptance_publish BEFORE INSERT ON councils.score_summary_idempotency FOR EACH ROW EXECUTE FUNCTION councils.wait_acceptance_publish();
            """);
        await using var blocker = MigrationDb(); await using var gate = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(710106)");
        var publication = PublishScoresAsync(body); Task? changing = null;
        try
        {
            await WaitForScoreSqlLockAsync("score_summary_idempotency");
            changing = ReplaceScoreConfigurationAsync(); await WaitForScoreSqlLockAsync("score_weight_config"); Assert.False(changing.IsCompleted);
        }
        finally { await gate.CommitAsync(); }
        using var published = await publication; Assert.Equal(HttpStatusCode.OK, published.StatusCode); Assert.NotNull(changing); await changing;
        using var student = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={_groupId}", _studentId, "Student");
        Assert.Equal(7.57m, (await ArchivalDataAsync(student)).GetProperty("finalScore").GetDecimal());
        await using var verify = Db<DefenseCouncilDbContext>(); Assert.Equal(ScoreSummaryStatus.PublishedToStudent, (await verify.ScoreSummarys.SingleAsync()).Status);
    }

    [Fact]
    public async Task ArchivalScore_cancelled_publication_rolls_back_state_receipt_and_audit()
    {
        await PrepareScoreCalculationAsync(); using var calculation = await CalculateAsync(); var calculated = await ArchivalDataAsync(calculation);
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION councils.cancel_acceptance_publish() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(710107); RETURN NEW; END $$;
            CREATE TRIGGER cancel_acceptance_publish BEFORE INSERT ON councils.score_summary_idempotency FOR EACH ROW EXECUTE FUNCTION councils.cancel_acceptance_publish();
            """);
        await using var blocker = MigrationDb(); await using var gate = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(710107)");
        using var request = new HttpRequestMessage(HttpMethod.Put, ScoresRoute + "/publish")
        { Content = JsonContent.Create(new { batchId = _batchId, selections = new[] { new { groupId = _groupId, expectedSummaryVersion = calculated.GetProperty("summaryVersion").GetUInt32() } } }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{_adminId:N}.Admin"); request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var cancellation = new CancellationTokenSource(); var publication = _client.SendAsync(request, cancellation.Token);
        try
        {
            await WaitForScoreSqlLockAsync("score_summary_idempotency"); cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publication);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)); await using var observer = MigrationDb();
            while (await observer.Database.SqlQueryRaw<bool>("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE pid<>pg_backend_pid() AND wait_event_type='Lock' AND query LIKE '%score_summary_idempotency%') AS \"Value\"").SingleAsync(timeout.Token))
                await Task.Delay(30, timeout.Token);
        }
        finally { cancellation.Cancel(); await gate.CommitAsync(); }
        await using var verify = Db<DefenseCouncilDbContext>(); Assert.Equal(ScoreSummaryStatus.Calculated, (await verify.ScoreSummarys.SingleAsync()).Status);
        Assert.Equal(0, await verify.ScoreSummaryIdempotencyRecords.CountAsync(r => r.Operation == "ScoreSummary.Publish"));
        Assert.Equal(0, await verify.AuditOutboxMessages.CountAsync(a => a.Action == "DefenseCouncil.ScoreSummary.Published"));
    }

    [Fact]
    public async Task ArchivalScore_student_in_two_batches_reads_only_the_selected_published_group()
    {
        await PrepareScoreCalculationAsync(); using var calculation = await CalculateAsync(); var calculated = await ArchivalDataAsync(calculation);
        var body = new { batchId = _batchId, selections = new[] { new { groupId = _groupId, expectedSummaryVersion = calculated.GetProperty("summaryVersion").GetUInt32() } } };
        using var published = await PublishScoresAsync(body); await ArchivalDataAsync(published);
        Guid batchId;
        await using (var academic = Db<AcademicPeriodsDbContext>())
        {
            var template = await academic.ScheduleTemplateVersions.Include(t => t.StageDefinitions).SingleAsync(t => t.TemplateCode == "FINAL_REPORT_WORKFLOW");
            var batch = KltnBatch.Create(Guid.NewGuid(), "Second batch", "2026-2027", 2, BatchStart, BatchStart.AddDays(146), template, _advisorId, SeedTime);
            academic.KltnBatches.Add(batch); await academic.SaveChangesAsync(); batchId = batch.Id;
        }
        var topicId = Guid.NewGuid();
        await using (var topics = Db<UGTM.Modules.TopicCatalog.Infrastructure.Persistence.TopicCatalogDbContext>())
        {
            topics.Topics.Add(new UGTM.Modules.TopicCatalog.Domain.Entities.Topic
            {
                Id = topicId,
                BatchId = batchId,
                AdvisorId = _advisorId,
                TitleVi = "Unpublished second batch",
                StudentNumber = 1,
                Status = UGTM.Modules.TopicCatalog.Domain.Enums.TopicStatus.Published,
                CreatedAt = SeedTime
            });
            await topics.SaveChangesAsync();
        }
        var other = new UGTM.Modules.ThesisManagement.Domain.Entities.StudentGroup(Guid.NewGuid(), topicId, SeedTime);
        other.AddConfirmedMember(_studentId, batchId, SeedTime);
        await using (var thesis = Db<UGTM.Modules.ThesisManagement.Infrastructure.Persistence.ThesisManagementDbContext>()) { thesis.StudentGroups.Add(other); await thesis.SaveChangesAsync(); }
        using var original = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={_groupId}", _studentId, "Student");
        Assert.Equal(_groupId, (await ArchivalDataAsync(original)).GetProperty("groupId").GetGuid());
        using var second = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={other.Id}", _studentId, "Student");
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("ScoreSummary.NotYetPublished", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
}
