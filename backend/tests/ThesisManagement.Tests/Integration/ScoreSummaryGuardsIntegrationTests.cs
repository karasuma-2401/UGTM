using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    [Theory]
    [InlineData("student")]
    [InlineData("lecturer")]
    [InlineData("academic")]
    public async Task ScoreSummary_private_reads_and_writes_use_live_roles_not_token_claims(string actorKind)
    {
        await PrepareScoreCalculationAsync();
        var actor = actorKind == "student" ? _studentId : actorKind == "lecturer" ? _scoreReviewerId : _scoreAcademicId;
        using var denied = await CalculateAsync(actor: actor, role: "Admin"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        foreach (var route in new[] { $"/group/{_groupId}", $"/readiness/{_groupId}", $"?batchId={_batchId}" })
        {
            using var response = await SendAsync(HttpMethod.Get, ScoresRoute + route, actor, "Admin");
            Assert.Equal(actorKind == "academic" ? (route.StartsWith("/group", StringComparison.Ordinal) ? HttpStatusCode.NotFound : HttpStatusCode.OK) : HttpStatusCode.Forbidden, response.StatusCode);
            if (actorKind != "academic") Assert.DoesNotContain("finalScore", await response.Content.ReadAsStringAsync());
        }
        using var discrepancy = await SendAsync(HttpMethod.Get, $"/api/councils/assignments/{_scoreAssignmentId}/discrepancy-check", _studentId, "Admin");
        Assert.Equal(HttpStatusCode.Forbidden, discrepancy.StatusCode);
        using var actualMember = await SendAsync(HttpMethod.Get, $"/api/councils/assignments/{_scoreAssignmentId}/discrepancy-check", _replacementAdvisorId, "Student");
        await ArchivalDataAsync(actualMember);
        using var nonmember = await SendAsync(HttpMethod.Get, $"/api/councils/assignments/{_scoreAssignmentId}/discrepancy-check", _scoreReviewerId, "Lecturer");
        Assert.Equal(HttpStatusCode.Forbidden, nonmember.StatusCode);
    }
    [Fact]
    public async Task ScoreSummary_manage_without_read_can_calculate_but_revocation_blocks_replay()
    {
        await PrepareScoreCalculationAsync();
        await using (var identity = Db<IdentityDbContext>()) await identity.RolePermissions.Where(p => p.Role.RoleName == "Faculty" && p.Permission.PermissionCode == "councils.score-summaries.read").ExecuteDeleteAsync();
        var key = Guid.NewGuid(); using var first = await CalculateAsync(key: key, actor: _scoreFacultyId, role: "Faculty"); await ArchivalDataAsync(first);
        using var read = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/group/{_groupId}", _scoreFacultyId, "Faculty"); Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        await using (var identity = Db<IdentityDbContext>()) await identity.RolePermissions.Where(p => p.Role.RoleName == "Faculty" && p.Permission.PermissionCode == "councils.score-summaries.manage").ExecuteDeleteAsync();
        using var replay = await CalculateAsync(key: key, actor: _scoreFacultyId, role: "Faculty"); Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
    }
    [Theory]
    [InlineData("Open", 200)]
    [InlineData("Closed", 200)]
    [InlineData("Draft", 409)]
    [InlineData("Archived", 409)]
    public async Task ScoreSummary_calculation_batch_policy_ignores_student_window(string status, int expected)
    {
        await PrepareScoreCalculationAsync(); _clock.UtcNow = _clock.UtcNow.AddYears(1);
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic.kltn_batch SET status={status} WHERE id={_batchId}");
        using var response = await CalculateAsync(); Assert.Equal((HttpStatusCode)expected, response.StatusCode);
        if (expected == 200) await ArchivalDataAsync(response);
    }
    [Theory]
    [InlineData("archive")]
    [InlineData("minute")]
    [InlineData("futureMinute")]
    [InlineData("advisor")]
    [InlineData("reviewer")]
    [InlineData("score")]
    [InlineData("config")]
    [InlineData("invalidConfig")]
    public async Task ScoreSummary_missing_prerequisites_never_persist_a_partial_result(string missing)
    {
        await PrepareScoreCalculationAsync(seal: missing != "archive");
        await using (var db = MigrationDb())
        {
            switch (missing)
            {
                case "minute": await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_minute SET lock_status='AllScoresEntered'"); break;
                case "futureMinute": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE councils.council_minute SET locked_at={_clock.UtcNow.AddDays(1)}"); break;
                case "advisor": await db.Database.ExecuteSqlRawAsync("UPDATE councils.advisor_evaluation SET is_legacy=true"); break;
                case "reviewer": await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_evaluation SET status='Draft',submitted_by=NULL,submitted_at_utc=NULL,is_visible_to_student=false"); break;
                case "score": await db.Database.ExecuteSqlRawAsync("DELETE FROM councils.evaluation_detail"); break;
                case "config": await db.Database.ExecuteSqlRawAsync("UPDATE academic.score_weight_config SET status='Retired' WHERE status='Active'"); break;
                case "invalidConfig": await db.Database.ExecuteSqlRawAsync("UPDATE academic.score_component_config SET source='InvalidSource'"); break;
            }
        }
        using var response = await CalculateAsync(); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var verify = Db<DefenseCouncilDbContext>(); Assert.Empty(await verify.ScoreSummarys.ToListAsync()); Assert.Empty(await verify.ScoreSummaryRevisions.ToListAsync()); Assert.Empty(await verify.ScoreSummaryIdempotencyRecords.ToListAsync());
    }
    [Theory]
    [InlineData("config")]
    [InlineData("sameScoreVersion")]
    [InlineData("minute")]
    [InlineData("ignored")]
    [InlineData("roster")]
    [InlineData("title")]
    public async Task ScoreSummary_read_stale_tracks_configuration_source_versions_and_context(string changed)
    {
        await PrepareScoreCalculationAsync(); using var calculated = await CalculateAsync(); var first = await ArchivalDataAsync(calculated);
        if (changed == "config") await ReplaceScoreConfigurationAsync();
        else await using (var db = MigrationDb())
        {
            switch (changed)
            {
                case "sameScoreVersion": await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_score SET score=score"); break;
                case "minute": await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_minute SET lock_status='Open'"); break;
                case "ignored": await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_score SET is_ignored_in_calculation=true,exclusion_reason='committee decision'"); break;
                case "roster": await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_member SET role_in_council=role_in_council"); break;
                case "title": await db.Database.ExecuteSqlRawAsync("UPDATE topics.topic SET title_vi='Title changed before publish'"); break;
            }
        }
        using var read = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/group/{_groupId}", _scoreAcademicId, "AcademicAffairs");
        var current = await ArchivalDataAsync(read); Assert.True(current.GetProperty("isStale").GetBoolean()); Assert.Equal(first.GetProperty("finalScore").GetDecimal(), current.GetProperty("finalScore").GetDecimal());
        using var readiness = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/readiness/{_groupId}", _adminId, "Admin"); Assert.False((await ArchivalDataAsync(readiness)).GetProperty("isReadyForPublication").GetBoolean());
        using var filtered = await SendAsync(HttpMethod.Get, $"{ScoresRoute}?batchId={_batchId}&isStale=true&pageSize=1", _adminId, "Admin"); Assert.Equal(1, (await ArchivalDataAsync(filtered)).GetProperty("totalCount").GetInt32());
    }
    [Fact]
    public async Task ScoreSummary_legacy_recalculation_preserves_original_and_pending_list_does_not_create_rows()
    {
        await PrepareScoreCalculationAsync();
        using var pending = await SendAsync(HttpMethod.Get, $"{ScoresRoute}?batchId={_batchId}&status=Pending", _scoreAcademicId, "AcademicAffairs");
        var list = await ArchivalDataAsync(pending); Assert.Equal(1, list.GetProperty("totalCount").GetInt32()); Assert.Equal(JsonValueKind.Null, list.GetProperty("items")[0].GetProperty("summaryId").ValueKind);
        Guid id;
        await using (var db = Db<DefenseCouncilDbContext>())
        {
            Assert.Empty(await db.ScoreSummarys.ToListAsync());
            var legacy = new ScoreSummary { Id = Guid.NewGuid(), GroupId = _groupId, FinalScore = 9, CalculationFormula = "legacy formula" }; id = legacy.Id;
            db.ScoreSummarys.Add(legacy); db.ScoreSummaryComponents.Add(new ScoreSummaryComponent { Id = Guid.NewGuid(), SummaryId = legacy.Id, ComponentCode = "OLD", SourceKey = "OLD", Score = 9, Weight = 1 }); await db.SaveChangesAsync();
        }
        using var missing = await CalculateAsync(); Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        using var oldDiscrepancy = await SendAsync(HttpMethod.Get, $"/api/councils/assignments/{_scoreAssignmentId}/discrepancy-check", _adminId, "Admin");
        Assert.Equal(JsonValueKind.Null, (await ArchivalDataAsync(oldDiscrepancy)).GetProperty("finalScore").ValueKind);
        using var read = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/group/{_groupId}", _adminId, "Admin"); var old = await ArchivalDataAsync(read); Assert.True(old.GetProperty("isLegacy").GetBoolean());
        using var recalculated = await CalculateAsync(old.GetProperty("summaryVersion").GetUInt32()); var result = await ArchivalDataAsync(recalculated); Assert.Equal(id, result.GetProperty("summaryId").GetGuid()); Assert.Equal(7.57m, result.GetProperty("finalScore").GetDecimal());
        using var currentDiscrepancy = await SendAsync(HttpMethod.Get, $"/api/councils/assignments/{_scoreAssignmentId}/discrepancy-check", _adminId, "Admin");
        Assert.Equal(7.57m, (await ArchivalDataAsync(currentDiscrepancy)).GetProperty("finalScore").GetDecimal());
        await using var verify = Db<DefenseCouncilDbContext>(); Assert.Contains("legacy formula", (await verify.ScoreSummarys.SingleAsync()).LegacySnapshot); Assert.Single(await verify.ScoreSummaryComponents.Where(c => c.RevisionId == null).ToListAsync());
    }
    [Fact]
    public async Task ScoreSummary_expected_versions_key_collision_and_concurrent_same_key_are_safe()
    {
        await PrepareScoreCalculationAsync();
        using var wrongConfig = await CalculateAsync(config: 1); Assert.Equal(HttpStatusCode.Conflict, wrongConfig.StatusCode);
        var key = Guid.NewGuid(); var responses = await Task.WhenAll(CalculateAsync(key: key), CalculateAsync(key: key));
        foreach (var response in responses) await ArchivalDataAsync(response);
        var first = await ArchivalDataAsync(responses[0]); Assert.Equal(await responses[0].Content.ReadAsStringAsync(), await responses[1].Content.ReadAsStringAsync()); foreach (var response in responses) response.Dispose();
        using var missingVersion = await CalculateAsync(); Assert.Equal(HttpStatusCode.Conflict, missingVersion.StatusCode);
        using var collision = await CalculateAsync(first.GetProperty("summaryVersion").GetUInt32(), key); Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode); Assert.Contains("IdempotencyConflict", await collision.Content.ReadAsStringAsync());
        using var recalc = await CalculateAsync(first.GetProperty("summaryVersion").GetUInt32()); await ArchivalDataAsync(recalc);
        using var stale = await CalculateAsync(first.GetProperty("summaryVersion").GetUInt32()); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlRawAsync("UPDATE academic.kltn_batch SET status='Archived'");
        using var replay = await CalculateAsync(key: key); Assert.Equal(first.GetProperty("summaryVersion").GetUInt32(), (await ArchivalDataAsync(replay)).GetProperty("summaryVersion").GetUInt32());
        await using var verify = Db<DefenseCouncilDbContext>(); Assert.Equal(2, await verify.ScoreSummaryRevisions.CountAsync()); Assert.Equal(2, await verify.ScoreSummaryIdempotencyRecords.CountAsync());
    }
    [Theory]
    [InlineData("receipt")]
    [InlineData("audit")]
    public async Task ScoreSummary_save_failure_rolls_back_root_revision_components_audit_and_receipt(string failure)
    {
        await PrepareScoreCalculationAsync();
        await using (var db = MigrationDb())
        {
            await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION councils.fail_score_save() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected score save failure'; END $$;");
            if (failure == "receipt") await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_score_save BEFORE INSERT ON councils.score_summary_idempotency FOR EACH ROW EXECUTE FUNCTION councils.fail_score_save()");
            else await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_score_save BEFORE INSERT ON councils.audit_outbox_message FOR EACH ROW EXECUTE FUNCTION councils.fail_score_save()");
        }
        using var response = await CalculateAsync(); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var verify = Db<DefenseCouncilDbContext>(); Assert.Empty(await verify.ScoreSummarys.ToListAsync()); Assert.Empty(await verify.ScoreSummaryRevisions.ToListAsync()); Assert.Empty(await verify.ScoreSummaryComponents.ToListAsync()); Assert.Empty(await verify.ScoreSummaryIdempotencyRecords.ToListAsync());
        Assert.Empty(await verify.AuditOutboxMessages.Where(a => a.Action == "DefenseCouncil.ScoreSummary.Calculated").ToListAsync());
    }
    [Fact]
    public async Task ScoreSummary_strict_HTTP_and_OpenAPI_do_not_accept_client_score_or_ambiguous_versions()
    {
        await PrepareScoreCalculationAsync();
        foreach (var raw in new[] { "[]", "{\"score\":10}", "{\"expectedSummaryVersion\":0}", "{\"expectedSummaryVersion\":\"1\"}", "{\"expectedActiveConfigVersion\":1.1}", "{\"expectedActiveConfigVersion\":0}", "{\"expectedSummaryVersion\":null,\"EXPECTEDSUMMARYVERSION\":null}" })
        { using var response = await CalculateAsync(raw: raw); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        using var query = await CalculateAsync(suffix: "?score=10"); Assert.Equal(HttpStatusCode.BadRequest, query.StatusCode);
        using var openapi = await _client.GetAsync("/openapi/v1.json"); using var document = JsonDocument.Parse(await openapi.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths"); var calculate = paths.GetProperty(ScoresRoute + "/calculate/{groupId}").GetProperty("post");
        Assert.Contains(calculate.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key" && p.GetProperty("required").GetBoolean());
        Assert.True(calculate.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
        var publish = paths.GetProperty(ScoresRoute + "/publish").GetProperty("put");
        Assert.Contains(publish.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key" && p.GetProperty("required").GetBoolean());
        var student = paths.GetProperty(ScoresRoute + "/my-group").GetProperty("get");
        Assert.Contains(student.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "groupId" && p.GetProperty("required").GetBoolean());
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var requestSchema = schemas.EnumerateObject().Single(s => s.Value.TryGetProperty("properties", out var properties)
            && properties.TryGetProperty("batchId", out _) && properties.TryGetProperty("selections", out _)).Value;
        var selections = requestSchema.GetProperty("properties").GetProperty("selections");
        Assert.Equal(1, selections.GetProperty("minItems").GetInt32()); Assert.Equal(100, selections.GetProperty("maxItems").GetInt32());
    }
    [Fact]
    public async Task ScoreSummary_frozen_source_and_history_guards_are_ready_for_future_publication_writer()
    {
        await PrepareScoreCalculationAsync(); var key = Guid.NewGuid(); using var calculated = await CalculateAsync(key: key); var first = await ArchivalDataAsync(calculated);
        // Publication is deliberately seeded to test CP3 guards. No claim that CP4 API exists yet.
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE councils.score_summary SET status='PublishedToStudent',published_revision_id=active_revision_id,published_by={_adminId},published_at={_clock.UtcNow}");
        using var denied = await CalculateAsync(first.GetProperty("summaryVersion").GetUInt32()); Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode); Assert.Contains("AlreadyPublished", await denied.Content.ReadAsStringAsync());
        using var replay = await CalculateAsync(key: key); Assert.Equal("Calculated", (await ArchivalDataAsync(replay)).GetProperty("status").GetString());
        foreach (var sql in new[]{"UPDATE councils.council_score SET score=score","UPDATE councils.council_minute SET lock_status='Open'","UPDATE councils.council_member SET role_in_council=role_in_council","UPDATE councils.evaluation_detail SET score=score","UPDATE councils.advisor_evaluation SET score=score","UPDATE councils.reviewer_evaluation SET score=score",
            "UPDATE councils.council_assignment SET group_id=gen_random_uuid()","UPDATE councils.council_score SET council_assignment_id=gen_random_uuid()"})
        { await using var db = MigrationDb(); var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql)); Assert.Equal(Npgsql.PostgresErrorCodes.LockNotAvailable, error.SqlState); }
        await using (var db = MigrationDb())
        {
            var frozen = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE councils.score_summary SET final_score=10")); Assert.Equal("23514", frozen.SqlState);
            var revisionError = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE councils.score_summary_revision SET final_score=10"));
            Assert.Equal("23514", revisionError.SqlState);
            var componentError = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE councils.score_summary_component SET score=10"));
            Assert.Equal("23514", componentError.SqlState);
        }
        using var source = await SendAsync(HttpMethod.Post, $"/api/councils/assignments/{_scoreAssignmentId}/scores", _adminId, "Admin", System.Net.Http.Json.JsonContent.Create(new { details = new[] { new { criteriaId = _scoreCriteriaId, score = 9 } }, feedback = "no overwrite" }));
        Assert.Equal(HttpStatusCode.Conflict, source.StatusCode); Assert.Contains("Council.ScoresAlreadyPublished", await source.Content.ReadAsStringAsync());
        await ReplaceScoreConfigurationAsync();
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlRawAsync("UPDATE topics.topic SET title_vi='Title changed after publish'");
        using var read = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/group/{_groupId}", _adminId, "Admin"); var published = await ArchivalDataAsync(read); Assert.False(published.GetProperty("isStale").GetBoolean());
        Assert.Contains("Final report workflow", published.GetProperty("sourceSnapshot").GetString());
        Assert.DoesNotContain("Title changed after publish", published.GetProperty("sourceSnapshot").GetString());
        using var scope = _app.Services.CreateScope(); Assert.Equal(ArchiveProofOutcome.Succeeded, (await scope.ServiceProvider.GetRequiredService<IFinalSubmissionArchiveProofContract>().GetArchiveProofAsync(_groupId)).Outcome);
    }
    [Fact]
    public async Task ScoreSummary_migration_legacy_roundtrip_retains_values_system_xmin_and_refuses_down_with_revisions()
    {
        await PrepareScoreCalculationAsync();
        Guid id;
        await using (var db = Db<DefenseCouncilDbContext>()) { var legacy = new ScoreSummary { Id = Guid.NewGuid(), GroupId = _groupId, FinalScore = 9, CalculationFormula = "legacy preserved" }; id = legacy.Id; db.ScoreSummarys.Add(legacy); await db.SaveChangesAsync(); }
        await using (var db = MigrationDb())
        { var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261102061935_AddFinalSubmissionArchivalWorkflow"); Assert.Equal(9m, await db.Database.SqlQueryRaw<decimal>("SELECT final_score AS \"Value\" FROM councils.score_summary").SingleAsync()); await migrator.MigrateAsync(); }
        uint version;
        await using (var db = Db<DefenseCouncilDbContext>()) { var row = await db.ScoreSummarys.SingleAsync(); Assert.Equal(id, row.Id); Assert.True(row.IsLegacy); version = row.Version; Assert.True(version > 0); }
        using var calculated = await CalculateAsync(version); await ArchivalDataAsync(calculated);
        await using (var db = MigrationDb()) await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261102061935_AddFinalSubmissionArchivalWorkflow"));
    }
    [Fact]
    public async Task ScoreSummary_migration_preflight_rejects_bad_legacy_before_schema_changes()
    {
        await using var db = MigrationDb(); var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261102061935_AddFinalSubmissionArchivalWorkflow");
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO councils.score_summary(id,group_id,status) VALUES({id},{_groupId},'InvalidLegacyStatus')");
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync());
        Assert.False(await db.Database.SqlQueryRaw<bool>("SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema='councils' AND table_name='score_summary_revision') AS \"Value\"").SingleAsync());
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE councils.score_summary SET status='Pending' WHERE id={id}"); await migrator.MigrateAsync();
        await using var defense = Db<DefenseCouncilDbContext>(); Assert.True((await defense.ScoreSummarys.SingleAsync()).IsLegacy);
    }
}
