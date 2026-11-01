using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Database.Migrations;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Theory]
    [InlineData("advisor", "NotEligible")]
    [InlineData("batch", "BatchNotOpen")]
    [InlineData("group", "NotEligible")]
    [InlineData("binding", "NotEligible")]
    [InlineData("account", "NotEligible")]
    [InlineData("role", "NotEligible")]
    public async Task Evaluation_cp2_new_intent_reads_current_guards_and_admin_cannot_bypass_them(string change, string code)
    {
        var parent = await CreateForCancellationAsync();
        await using (var db = Db<UGTMDbContext>()) await db.Database.ExecuteSqlInterpolatedAsync(Change(change));
        using var response = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, action: "Submit", score: 8, comment: "Góp ý", decision: "Approved"));
        await EvaluationErrorAsync(response, HttpStatusCode.Conflict, code);
        await AssertEvaluationCountsAsync(0, 0, 0);
    }

    [Fact]
    public async Task Evaluation_cp2_versions_prevent_phantom_create_missing_or_stale_updates_and_locked_precedes_guard_errors()
    {
        var parent = await CreateForCancellationAsync();
        using (var missing = await SaveEvaluationAsync(EvaluationBody(Guid.NewGuid(), parent.Version))) await EvaluationErrorAsync(missing, HttpStatusCode.NotFound, "AssignmentNotFound");
        using (var stale = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version + 1))) await EvaluationErrorAsync(stale, HttpStatusCode.Conflict, "AssignmentVersionConflict");
        using (var phantom = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, 123))) await EvaluationErrorAsync(phantom, HttpStatusCode.Conflict, "VersionConflict");
        using var draft = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version));
        var version = (await EvaluationDataAsync(draft)).GetProperty("evaluationVersion").GetUInt32();
        foreach (var expected in new uint?[] { null, version + 1 })
        {
            using var wrong = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, expected));
            await EvaluationErrorAsync(wrong, HttpStatusCode.Conflict, "VersionConflict");
        }
        using var submit = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, version, "Submit", 8, "Góp ý", "Approved"));
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        await using (var db = Db<UGTMDbContext>())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(Change("batch"));
            await db.Database.ExecuteSqlInterpolatedAsync(Change("binding"));
        }
        using var locked = await SaveEvaluationAsync(EvaluationBody(parent.Id, 1));
        await EvaluationErrorAsync(locked, HttpStatusCode.Conflict, "Locked");
        await AssertEvaluationCountsAsync(1, 2, 2);
    }

    [Fact]
    public async Task Evaluation_cp2_replay_canonicalizes_intent_preserves_old_status_and_does_not_reopen_submitted_or_require_open_batch()
    {
        var key = Guid.NewGuid(); var parent = await CreateForCancellationAsync(key);
        var originalBody = EvaluationBody(parent.Id, parent.Version, score: 8.345m, comment: "  Dòng 1\nDòng 2: \"null\"  ", decision: " approved ");
        using var request = EvaluationRequest(originalBody, key); // Same UUID as SCRUM-104 receipt is independent.
        request.Headers.Add("X-Test-Discard-Response", "true");
        using var lost = await (await HttpAsync()).SendAsync(request); Assert.Equal(HttpStatusCode.ServiceUnavailable, lost.StatusCode);
        using var retry = await SaveEvaluationAsync(originalBody, key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var originalPayload = await retry.Content.ReadAsStringAsync(); var first = await EvaluationDataAsync(retry);
        var normalizedBody = EvaluationBody(parent.Id, parent.Version, score: 8.35m, comment: "Dòng 1\nDòng 2: \"null\"", decision: "Approved");
        using (var normalized = await SaveEvaluationAsync(normalizedBody, key)) Assert.Equal(originalPayload, await normalized.Content.ReadAsStringAsync());
        using (var reordered = EvaluationRequest(originalBody, key))
        {
            var json = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["COMMENT"] = "Dòng 1\nDòng 2: \"null\"",
                ["SCORE"] = 8.35m,
                ["ACTION"] = " savedraft ",
                ["assignmentId"] = parent.Id.ToString().ToUpperInvariant(),
                ["defenseApproval"] = "APPROVED",
                ["expectedAssignmentVersion"] = parent.Version
            });
            reordered.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await (await HttpAsync()).SendAsync(reordered);
            Assert.Equal(originalPayload, await response.Content.ReadAsStringAsync());
        }
        foreach (var changed in new[] {
            EvaluationBody(parent.Id, parent.Version, score: 9, comment: "Dòng 1\nDòng 2: \"null\"", decision: "Approved"),
            EvaluationBody(parent.Id, parent.Version, action: "Submit", score: 8.35m, comment: "Dòng 1\nDòng 2: \"null\"", decision: "Approved"),
            EvaluationBody(parent.Id, parent.Version, 1, score: 8.35m, comment: "Dòng 1\nDòng 2: \"null\"", decision: "Approved"),
            EvaluationBody(parent.Id, parent.Version, score: 8.35m, comment: "null", decision: "Approved") })
        {
            using var conflict = await SaveEvaluationAsync(changed, key); await EvaluationErrorAsync(conflict, HttpStatusCode.Conflict, "IdempotencyConflict");
        }
        using (var invalid = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, score: 10.1m), key))
            await EvaluationErrorAsync(invalid, HttpStatusCode.BadRequest, "InvalidScore");
        using var submit = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, first.GetProperty("evaluationVersion").GetUInt32(), "Submit", 7, "Chốt", "Rejected"));
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        await using (var db = Db<UGTMDbContext>()) await db.Database.ExecuteSqlInterpolatedAsync(Change("batch"));
        using (var oldReplay = await SaveEvaluationAsync(originalBody, key))
        {
            Assert.Equal(HttpStatusCode.Created, oldReplay.StatusCode); Assert.Equal(originalPayload, await oldReplay.Content.ReadAsStringAsync());
        }
        using (var read = await ReadEvaluationAsync(parent.Id)) Assert.Equal("Submitted", (await EvaluationDataAsync(read)).GetProperty("status").GetString());
        await AssertEvaluationCountsAsync(1, 2, 2);
    }

    [Fact]
    public async Task Evaluation_cp2_receipt_scope_is_actor_specific_and_replay_never_grants_revoked_reviewer_access()
    {
        var parent = await CreateForCancellationAsync(); var key = Guid.NewGuid();
        var body = EvaluationBody(parent.Id, parent.Version);
        using var own = await SaveEvaluationAsync(body, key, "Lecturer", _reviewer);
        var version = (await EvaluationDataAsync(own)).GetProperty("evaluationVersion").GetUInt32();
        using var admin = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, version, comment: "Admin sửa nháp"), key);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        await using (var db = Db<UGTMDbContext>()) await db.Database.ExecuteSqlInterpolatedAsync(Change("role"));
        using var revoked = await SaveEvaluationAsync(body, key, "Lecturer", _reviewer);
        await EvaluationErrorAsync(revoked, HttpStatusCode.Forbidden, "AccessDenied");
        await AssertEvaluationCountsAsync(1, 2, 2);
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Equal(2, await verify.ReviewerEvaluationIdempotencyRecords.Select(value => value.ActorId).Distinct().CountAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Evaluation_cp2_either_save_failure_rolls_back_evaluation_receipt_and_audit_and_same_key_can_retry(bool secondSave, bool existingDraft)
    {
        var parent = await CreateForCancellationAsync(); uint? version = null;
        if (existingDraft)
        {
            using var draft = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version));
            version = (await EvaluationDataAsync(draft)).GetProperty("evaluationVersion").GetUInt32();
        }
        var body = EvaluationBody(parent.Id, parent.Version, version, "Submit", 8, "Góp ý", "Approved");
        var key = Guid.NewGuid();
        await using var db = Db<UGTMDbContext>();
        await db.Database.ExecuteSqlRawAsync(secondSave ? existingDraft ? """
            ALTER TABLE councils.reviewer_evaluation_idempotency ADD CONSTRAINT force_evaluation_failure CHECK (response_status_code = 201 OR response_payload_json = '{{}}'::jsonb)
            """ : """
            ALTER TABLE councils.reviewer_evaluation_idempotency ADD CONSTRAINT force_evaluation_failure CHECK (response_payload_json = '{{}}'::jsonb)
            """ : """
            ALTER TABLE councils.reviewer_evaluation ADD CONSTRAINT force_evaluation_failure CHECK (score IS NULL)
            """);
        using (var failed = await SaveEvaluationAsync(body, key))
        {
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.DoesNotContain("force_evaluation_failure", await failed.Content.ReadAsStringAsync());
        }
        var priorCount = existingDraft ? 1 : 0;
        await AssertEvaluationCountsAsync(priorCount, priorCount, priorCount);
        if (existingDraft)
        {
            await using var before = Db<DefenseCouncilDbContext>(); var unchanged = await before.ReviewerEvaluations.SingleAsync();
            Assert.Equal(version, unchanged.Version); Assert.Equal(UGTM.Modules.DefenseCouncil.Domain.Enums.ReviewerEvaluationStatus.Draft, unchanged.Status);
            Assert.Null(unchanged.Score); Assert.False(unchanged.IsVisibleToStudent);
        }
        await db.Database.ExecuteSqlRawAsync(secondSave
            ? "ALTER TABLE councils.reviewer_evaluation_idempotency DROP CONSTRAINT force_evaluation_failure"
            : "ALTER TABLE councils.reviewer_evaluation DROP CONSTRAINT force_evaluation_failure");
        using var retry = await SaveEvaluationAsync(body, key);
        Assert.Equal(existingDraft ? HttpStatusCode.OK : HttpStatusCode.Created, retry.StatusCode);
        await AssertEvaluationCountsAsync(1, priorCount + 1, priorCount + 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Evaluation_cp2_concurrent_creates_serialize_by_parent_or_key_without_duplicate_audit(bool sameKey)
    {
        var parent = await CreateForCancellationAsync(); var body = EvaluationBody(parent.Id, parent.Version); var key = Guid.NewGuid();
        await using var blocker = Db<DefenseCouncilDbContext>(); await using var gate = await blocker.Database.BeginTransactionAsync();
        var backend = await blocker.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM councils.reviewer_assignment WHERE id={parent.Id} FOR UPDATE");
        var first = SaveEvaluationAsync(body, key);
        var firstBackend = await FindAssignmentWaiterAsync(backend);
        var second = SaveEvaluationAsync(body, sameKey ? key : Guid.NewGuid());
        try { await FindBlockedEvaluationBackendAsync(firstBackend); }
        finally { await gate.RollbackAsync(); }
        var responses = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30));
        try
        {
            Assert.Contains(responses, value => value.StatusCode == HttpStatusCode.Created);
            if (sameKey)
            {
                Assert.All(responses, value => Assert.Equal(HttpStatusCode.Created, value.StatusCode));
                Assert.Equal(await responses[0].Content.ReadAsStringAsync(), await responses[1].Content.ReadAsStringAsync());
            }
            else await EvaluationErrorAsync(responses.Single(value => value.StatusCode != HttpStatusCode.Created), HttpStatusCode.Conflict, "VersionConflict");
        }
        finally { foreach (var response in responses) response.Dispose(); }
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Evaluation_cp2_concurrent_edit_and_submit_never_overwrite_a_stale_draft(bool submit, bool firstSubmits)
    {
        var parent = await CreateForCancellationAsync(); using var draft = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version));
        var version = (await EvaluationDataAsync(draft)).GetProperty("evaluationVersion").GetUInt32();
        var firstBody = EvaluationBody(parent.Id, parent.Version, version, firstSubmits ? "Submit" : "SaveDraft", firstSubmits ? 8 : null, "Bản sửa thắng", firstSubmits ? "Approved" : null);
        var secondBody = EvaluationBody(parent.Id, parent.Version, version, submit ? "Submit" : "SaveDraft", 9, "Bản cũ không được ghi đè", "Approved");
        await using var blocker = Db<DefenseCouncilDbContext>(); await using var gate = await blocker.Database.BeginTransactionAsync();
        var backend = await blocker.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM councils.reviewer_assignment WHERE id={parent.Id} FOR UPDATE");
        var first = SaveEvaluationAsync(firstBody); await FindAssignmentWaiterAsync(backend);
        var second = SaveEvaluationAsync(secondBody);
        try { await FindBlockedEvaluationBackendAsync(await FindAssignmentWaiterAsync(backend)); } finally { await gate.RollbackAsync(); }
        using var won = await first.WaitAsync(TimeSpan.FromSeconds(30)); Assert.Equal(HttpStatusCode.OK, won.StatusCode);
        using var lost = await second.WaitAsync(TimeSpan.FromSeconds(30)); await EvaluationErrorAsync(lost, HttpStatusCode.Conflict, firstSubmits ? "Locked" : "VersionConflict");
        using var read = await ReadEvaluationAsync(parent.Id); Assert.Equal("Bản sửa thắng", (await EvaluationDataAsync(read)).GetProperty("comment").GetString());
        await AssertEvaluationCountsAsync(1, 2, 2);
    }

    [Fact]
    public async Task Evaluation_cp2_replay_rechecks_live_reviewer_access_after_waiting_on_the_request_key()
    {
        var parent = await CreateForCancellationAsync(); var key = Guid.NewGuid(); var body = EvaluationBody(parent.Id, parent.Version);
        using var saved = await SaveEvaluationAsync(body, key, "Lecturer", _reviewer); Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        await using var scope = _app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DefenseCouncilDbContext>();
        await using var gate = await db.Database.BeginTransactionAsync();
        await scope.ServiceProvider.GetRequiredService<IReviewerEvaluationIdempotencyRepository>().LockRequestAsync(_reviewer, "ReviewerEvaluation.Save", key);
        var backend = await db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        var replaying = SaveEvaluationAsync(body, key, "Lecturer", _reviewer);
        try
        {
            await FindBlockedEvaluationBackendAsync(backend);
            await using var raw = Db<UGTMDbContext>(); await raw.Database.ExecuteSqlInterpolatedAsync(Change("account"));
        }
        finally { await gate.RollbackAsync(); }
        using var revoked = await replaying.WaitAsync(TimeSpan.FromSeconds(30)); await EvaluationErrorAsync(revoked, HttpStatusCode.Forbidden, "AccessDenied");
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Evaluation_cp2_handler_maps_a_real_postgres_lock_timeout_to_conflict_without_mutation()
    {
        var parent = await CreateForCancellationAsync();
        await using var blocker = Db<DefenseCouncilDbContext>(); await using var gate = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM councils.reviewer_assignment WHERE id={parent.Id} FOR UPDATE");
        await using var scope = _app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DefenseCouncilDbContext>();
        await db.Database.OpenConnectionAsync(); await db.Database.ExecuteSqlRawAsync("SET lock_timeout = '150ms'");
        try
        {
            var result = await scope.ServiceProvider.GetRequiredService<SaveReviewerEvaluationHandler>()
                .HandleAsync(new(parent.Id, ReviewerEvaluationAction.SaveDraft, parent.Version, null, null, null, null, Guid.NewGuid()));
            Assert.Equal("ReviewerEvaluation.ConcurrencyConflict", result.Error.Code);
        }
        finally { await db.Database.ExecuteSqlRawAsync("RESET lock_timeout"); await db.Database.CloseConnectionAsync(); }
        await AssertEvaluationCountsAsync(0, 0, 0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Evaluation_cp2_http_save_and_cancel_observe_the_same_parent_lock_in_both_orders(bool evaluationWins, bool submit)
    {
        var parent = await CreateForCancellationAsync();
        var body = EvaluationBody(parent.Id, parent.Version, action: submit ? "Submit" : "SaveDraft", score: submit ? 8 : null,
            comment: submit ? "Kết luận" : null, decision: submit ? "Approved" : null);
        await using var blocker = Db<DefenseCouncilDbContext>(); await using var gate = await blocker.Database.BeginTransactionAsync();
        var backend = await blocker.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM councils.reviewer_assignment WHERE id={parent.Id} FOR UPDATE");
        Task<HttpResponseMessage> writing, cancelling;
        if (evaluationWins)
        {
            writing = SaveEvaluationAsync(body); var writer = await FindAssignmentWaiterAsync(backend);
            cancelling = DeleteAsync(parent.Id, parent.Version);
            try { await FindBlockedEvaluationBackendAsync(writer); } finally { await gate.RollbackAsync(); }
        }
        else
        {
            cancelling = DeleteAsync(parent.Id, parent.Version); var cancel = await FindAssignmentWaiterAsync(backend);
            writing = SaveEvaluationAsync(body);
            try { await FindBlockedEvaluationBackendAsync(cancel); } finally { await gate.RollbackAsync(); }
        }
        using var saved = await writing.WaitAsync(TimeSpan.FromSeconds(30)); using var cancelled = await cancelling.WaitAsync(TimeSpan.FromSeconds(30));
        if (evaluationWins)
        {
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode); await AssertErrorAsync(cancelled, HttpStatusCode.Conflict, "EvaluationExists");
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode); await EvaluationErrorAsync(saved, HttpStatusCode.Conflict, "NotEligible");
        }
        await AssertEvaluationCountsAsync(evaluationWins ? 1 : 0, evaluationWins ? 1 : 0, evaluationWins ? 1 : 0);
        await using var db = Db<DefenseCouncilDbContext>(); Assert.Equal(evaluationWins, (await db.ReviewerAssignments.SingleAsync()).IsActive);
    }

    private async Task<int> FindBlockedEvaluationBackendAsync(int blocker)
    {
        await using var observer = Db<UGTMDbContext>(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            var rows = await observer.Database.SqlQuery<int>($"SELECT pid AS \"Value\" FROM pg_stat_activity WHERE {blocker}=ANY(pg_blocking_pids(pid)) ORDER BY pid LIMIT 1").ToListAsync(timeout.Token);
            if (rows.Count > 0) return rows[0];
            await Task.Delay(25, timeout.Token);
        }
    }
}
