using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    private async Task<HttpResponseMessage> PublishScoresAsync(object body, Guid? key = null, Guid? actor = null, string role = "Admin", string? raw = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, ScoresRoute + "/publish")
        { Content = raw is null ? JsonContent.Create(body) : new StringContent(raw, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{actor ?? _adminId:N}.{role}");
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task ScorePublication_publishes_verified_snapshot_for_own_student_and_replays_without_duplicate_audit()
    {
        await PrepareScoreCalculationAsync();
        using var before = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={_groupId}", _studentId, "Student");
        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        using var calculated = await CalculateAsync(); var data = await ArchivalDataAsync(calculated);
        var version = data.GetProperty("summaryVersion").GetUInt32(); var key = Guid.NewGuid();
        var body = new { batchId = _batchId, selections = new[] { new { groupId = _groupId, expectedSummaryVersion = version } } };
        using var publication = await PublishScoresAsync(body, key, _scoreFacultyId, "Faculty");
        var published = await ArchivalDataAsync(publication); Assert.Equal(1, published.GetProperty("publishedCount").GetInt32());
        using var replay = await PublishScoresAsync(body, key, _scoreFacultyId, "Faculty");
        Assert.Equal(await publication.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var collision = await PublishScoresAsync(new { batchId = Guid.NewGuid(), selections = body.selections }, key, _scoreFacultyId, "Faculty");
        Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        using var own = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={_groupId}", _studentId, "Student");
        var student = await ArchivalDataAsync(own); Assert.Equal(7.57m, student.GetProperty("finalScore").GetDecimal());
        Assert.Equal("Final report workflow", student.GetProperty("topicTitle").GetString());
        Assert.Equal(new[] { "calculationFormula", "components", "finalScore", "groupId", "publishedAt", "status", "topicTitle" }, student.EnumerateObject().Select(p => p.Name).Order().ToArray());
        foreach (var component in student.GetProperty("components").EnumerateArray())
            Assert.Equal(new[] { "componentName", "score", "weight" }, component.EnumerateObject().Select(p => p.Name).Order().ToArray());
        using var outsider = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={_groupId}", _scoreFacultyId, "Faculty"); Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
        using var badSelector = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={_groupId}&groupId={_groupId}", _studentId, "Student"); Assert.Equal(HttpStatusCode.BadRequest, badSelector.StatusCode);
        await using (var db = MigrationDb())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE topics.topic SET title_vi='Changed after publication'");
            await db.Database.ExecuteSqlRawAsync("UPDATE academic.kltn_batch SET status='Archived'");
        }
        using var archived = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={_groupId}", _studentId, "Student");
        Assert.Equal("Final report workflow", (await ArchivalDataAsync(archived)).GetProperty("topicTitle").GetString());
        using var replayArchived = await PublishScoresAsync(body, key, _scoreFacultyId, "Faculty"); Assert.Equal(HttpStatusCode.OK, replayArchived.StatusCode);
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Equal(1, await verify.AuditOutboxMessages.CountAsync(a => a.Action == "DefenseCouncil.ScoreSummary.Published"));
        Assert.Equal(1, await verify.ScoreSummaryIdempotencyRecords.CountAsync(r => r.Operation == "ScoreSummary.Publish"));
        var summary = await verify.ScoreSummarys.SingleAsync(); Assert.Equal(ScoreSummaryStatus.PublishedToStudent, summary.Status); Assert.Equal(summary.ActiveRevisionId, summary.PublishedRevisionId);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("config")]
    public async Task ScorePublication_rejects_whole_selection_on_missing_or_stale_and_checks_live_permissions(string changed)
    {
        await PrepareScoreCalculationAsync(); using var calculated = await CalculateAsync(); var data = await ArchivalDataAsync(calculated);
        var version = data.GetProperty("summaryVersion").GetUInt32(); var missing = Guid.NewGuid();
        var selected = new[] { new { groupId = _groupId, expectedSummaryVersion = version }, new { groupId = missing, expectedSummaryVersion = 1u } };
        using var denied = await PublishScoresAsync(new { batchId = _batchId, selections = selected }, actor: _scoreAcademicId, role: "AcademicAffairs"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var invalid = await PublishScoresAsync(new { batchId = _batchId, selections = selected }); Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        using (var json = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync())) Assert.True(json.RootElement.GetProperty("error").GetProperty("errors").TryGetProperty(missing.ToString(), out _));
        await using (var db = Db<DefenseCouncilDbContext>()) Assert.Equal(ScoreSummaryStatus.Calculated, (await db.ScoreSummarys.SingleAsync()).Status);
        if (changed == "config") await ReplaceScoreConfigurationAsync();
        else { await using var db = MigrationDb(); await db.Database.ExecuteSqlRawAsync("UPDATE topics.topic SET title_vi='Changed before publication'"); }
        using var stale = await PublishScoresAsync(new { batchId = _batchId, selections = selected.Take(1).ToArray() }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var json = JsonDocument.Parse(await stale.Content.ReadAsStringAsync())) Assert.Equal("ScoreSummary.Stale", json.RootElement.GetProperty("error").GetProperty("errors").GetProperty(_groupId.ToString())[0].GetString());
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Equal(ScoreSummaryStatus.Calculated, (await verify.ScoreSummarys.SingleAsync()).Status);
        Assert.Equal(0, await verify.ScoreSummaryIdempotencyRecords.CountAsync(r => r.Operation == "ScoreSummary.Publish"));
    }

    [Theory]
    [InlineData("receipt")]
    [InlineData("audit")]
    public async Task ScorePublication_rejects_bad_payload_and_rolls_back_on_write_failure(string failure)
    {
        await PrepareScoreCalculationAsync(); using var calculated = await CalculateAsync(); var data = await ArchivalDataAsync(calculated);
        var version = data.GetProperty("summaryVersion").GetUInt32(); var body = new { batchId = _batchId, selections = new[] { new { groupId = _groupId, expectedSummaryVersion = version } } };
        foreach (var raw in new[] { "{}", "[]", "{\"batchId\":null,\"selections\":[]}", "{\"batchId\":\"" + _batchId + "\",\"selections\":[{\"groupId\":\"" + _groupId + "\",\"expectedSummaryVersion\":\"1\"}]}" })
        { using var bad = await PublishScoresAsync(body, raw: raw); Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode); }
        var duplicate = new { batchId = _batchId, selections = new[] { body.selections[0], body.selections[0] } };
        using var duplicated = await PublishScoresAsync(duplicate); Assert.Equal(HttpStatusCode.BadRequest, duplicated.StatusCode);
        await using (var db = MigrationDb())
        {
            await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION councils.fail_publish_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected'; END $$;");
            if (failure == "receipt") await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_publish_receipt BEFORE INSERT ON councils.score_summary_idempotency FOR EACH ROW EXECUTE FUNCTION councils.fail_publish_receipt()");
            else await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_publish_receipt BEFORE INSERT ON councils.audit_outbox_message FOR EACH ROW EXECUTE FUNCTION councils.fail_publish_receipt()");
        }
        using var failed = await PublishScoresAsync(body); Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await using var verify = Db<DefenseCouncilDbContext>(); Assert.Equal(ScoreSummaryStatus.Calculated, (await verify.ScoreSummarys.SingleAsync()).Status);
        Assert.Equal(0, await verify.AuditOutboxMessages.CountAsync(a => a.Action == "DefenseCouncil.ScoreSummary.Published"));
    }

    [Fact]
    public async Task ScorePublication_concurrent_same_key_is_single_commit_and_student_does_not_see_ignored_component()
    {
        await PrepareScoreCalculationAsync();
        await using (var db = Db<DefenseCouncilDbContext>())
        {
            var score = await db.CouncilScores.OrderBy(s => s.Score).FirstAsync();
            score.IsIgnoredInCalculation = true; score.ExclusionReason = "Not recognized"; await db.SaveChangesAsync();
        }
        using var calculated = await CalculateAsync(); var result = await ArchivalDataAsync(calculated);
        var body = new { batchId = _batchId, selections = new[] { new { groupId = _groupId, expectedSummaryVersion = result.GetProperty("summaryVersion").GetUInt32() } } };
        var key = Guid.NewGuid(); var calls = await Task.WhenAll(PublishScoresAsync(body, key), PublishScoresAsync(body, key));
        using var first = calls[0]; using var second = calls[1];
        Assert.Equal(HttpStatusCode.OK, first.StatusCode); Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        using var own = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={_groupId}", _studentId, "Student");
        var student = await ArchivalDataAsync(own); Assert.Equal(4, student.GetProperty("components").GetArrayLength());
        Assert.DoesNotContain("Not recognized", student.GetRawText());
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Equal(1, await verify.AuditOutboxMessages.CountAsync(a => a.Action == "DefenseCouncil.ScoreSummary.Published"));
        Assert.Equal(1, await verify.ScoreSummaryIdempotencyRecords.CountAsync(r => r.Operation == "ScoreSummary.Publish"));
    }

    [Fact]
    public async Task ScorePublication_holds_source_locks_until_commit_and_rejects_concurrent_source_write()
    {
        await PrepareScoreCalculationAsync();
        using var calculated = await CalculateAsync(); var result = await ArchivalDataAsync(calculated);
        var body = new { batchId = _batchId, selections = new[] { new { groupId = _groupId, expectedSummaryVersion = result.GetProperty("summaryVersion").GetUInt32() } } };
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION councils.wait_publication_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(710105); RETURN NEW; END $$;
            CREATE TRIGGER wait_publication_receipt BEFORE INSERT ON councils.score_summary_idempotency FOR EACH ROW EXECUTE FUNCTION councils.wait_publication_receipt();
            """);
        await using var blocker = MigrationDb(); await using var gate = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(710105)");
        var publication = PublishScoresAsync(body); Task? changing = null;
        async Task ChangeMinuteAsync()
        { await using var db = MigrationDb(); await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_minute SET general_feedback='late change'"); }
        try
        {
            await WaitForScoreSqlLockAsync("score_summary_idempotency");
            changing = ChangeMinuteAsync(); await WaitForScoreSqlLockAsync("UPDATE councils.council_minute"); Assert.False(changing.IsCompleted);
        }
        finally { await gate.CommitAsync(); }
        using var response = await publication; Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(changing);
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(async () => await changing);
        Assert.Equal("55P03", error.SqlState);
        await using var verify = Db<DefenseCouncilDbContext>(); Assert.Equal(ScoreSummaryStatus.PublishedToStudent, (await verify.ScoreSummarys.SingleAsync()).Status);
    }

    [Fact]
    public async Task ScorePublication_publishes_two_valid_groups_atomically_and_normalizes_selection_order()
    {
        await PrepareScoreCalculationAsync();
        using var firstCalculation = await CalculateAsync(); var first = await ArchivalDataAsync(firstCalculation);
        var firstGroup = _groupId; var firstVersion = first.GetProperty("summaryVersion").GetUInt32();
        var archiveTime = _clock.UtcNow; var student = Guid.NewGuid(); var topic = Guid.NewGuid(); var group = Guid.NewGuid();
        await using (var identity = Db<UGTM.Modules.Identity.Infrastructure.Persistence.IdentityDbContext>())
        {
            var role = await identity.Roles.SingleAsync(r => r.RoleName == "Student");
            identity.Users.Add(UGTM.Modules.Identity.Domain.Entities.User.Create(student, $"cp4-{student:N}@test.example", $"CP4-{student:N}@TEST.EXAMPLE", "Student two", null, null, null, UGTM.Modules.Identity.Domain.Enums.AccountStatus.Active, SeedTime));
            identity.UserRoles.Add(new UGTM.Modules.Identity.Domain.Entities.UserRole { UserId = student, RoleId = role.Id, AssignedAt = SeedTime }); await identity.SaveChangesAsync();
        }
        await using (var topics = Db<UGTM.Modules.TopicCatalog.Infrastructure.Persistence.TopicCatalogDbContext>())
        {
            topics.Topics.Add(new UGTM.Modules.TopicCatalog.Domain.Entities.Topic { Id = topic, BatchId = _batchId, AdvisorId = _advisorId, TitleVi = "Second thesis", StudentNumber = 1, Status = UGTM.Modules.TopicCatalog.Domain.Enums.TopicStatus.Published, CreatedAt = SeedTime });
            await topics.SaveChangesAsync();
        }
        await using (var thesis = Db<UGTM.Modules.ThesisManagement.Infrastructure.Persistence.ThesisManagementDbContext>())
        {
            var next = new UGTM.Modules.ThesisManagement.Domain.Entities.StudentGroup(group, topic, SeedTime); next.AddConfirmedMember(student, _batchId, SeedTime);
            Assert.True(next.TrySubmitSyllabus()); Assert.True(next.TryApproveSyllabus()); Assert.True(next.TrySubmitMidtermReport()); Assert.True(next.TryApproveMidtermReport());
            var midterm = UGTM.Modules.ThesisManagement.Domain.Entities.MidtermReport.CreateSubmitted(Guid.NewGuid(), group);
            midterm.RecordReview(UGTM.Modules.ThesisManagement.Domain.Enums.ReviewDecision.Approved, null, _advisorId, SeedTime);
            thesis.StudentGroups.Add(next); thesis.MidtermReports.Add(midterm); await thesis.SaveChangesAsync();
        }
        _groupId = group; _topicId = topic; _clock.UtcNow = _opensAtUtc.AddHours(1);
        using var reportResponse = await SubmitAsync("pdf", student); await ArchivalDataAsync(reportResponse);
        Guid reportId; Guid fileId; string checksum;
        await using (var thesis = Db<UGTM.Modules.ThesisManagement.Infrastructure.Persistence.ThesisManagementDbContext>())
        {
            var report = await thesis.FinalReports.SingleAsync(r => r.GroupId == group); reportId = report.Id;
            var file = await thesis.SubmissionFiles.SingleAsync(f => f.EntityId == reportId); fileId = file.Id; checksum = file.ChecksumSha256!;
        }
        using var begin = await BeginAsync(reportId, 1, _advisorId); await ArchivalDataAsync(begin); _clock.UtcNow = archiveTime;
        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            var advisor = UGTM.Modules.DefenseCouncil.Domain.Entities.AdvisorEvaluation.Create(Guid.NewGuid(), group, reportId, 1, fileId, checksum, 8, "Advisor", _advisorId, archiveTime.AddDays(-3));
            Assert.True(advisor.TryDecide(true, null, _advisorId, archiveTime.AddDays(-3))); defense.AdvisorEvaluations.Add(advisor);
            var reviewer = UGTM.Modules.DefenseCouncil.Domain.Entities.ReviewerAssignment.Create(Guid.NewGuid(), group, _scoreReviewerId, _advisorId, advisor.Id, reportId, 1, fileId, checksum, _adminId, archiveTime.AddDays(-2));
            var evaluation = UGTM.Modules.DefenseCouncil.Domain.Entities.ReviewerEvaluation.CreateDraft(Guid.NewGuid(), reviewer.Id, 7, "Feedback", DefenseApprovalStatus.Approved, _scoreReviewerId, archiveTime.AddDays(-2));
            Assert.True(evaluation.TrySubmit(7, "Feedback", DefenseApprovalStatus.Approved, _scoreReviewerId, archiveTime.AddDays(-2)));
            defense.ReviewerAssignments.Add(reviewer); defense.ReviewerEvaluations.Add(evaluation);
            var council = await defense.Councils.SingleAsync();
            var assignment = new UGTM.Modules.DefenseCouncil.Domain.Entities.CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = group, OrderNumber = 2 };
            defense.CouncilAssignments.Add(assignment); defense.CouncilMinutes.Add(new UGTM.Modules.DefenseCouncil.Domain.Entities.CouncilMinute { Id = Guid.NewGuid(), CouncilAssignmentId = assignment.Id, LockStatus = CouncilMinuteLockStatus.Locked, LockedBy = _adminId, LockedAt = archiveTime.AddDays(-1) });
            foreach (var member in await defense.CouncilMembers.Where(m => m.CouncilId == council.Id && m.UserId != _scoreReviewerId).ToListAsync())
            {
                var score = member.RoleInCouncil == CouncilMemberRole.Chairman ? 9m : member.RoleInCouncil == CouncilMemberRole.Secretary ? 8m : 6m;
                var row = new UGTM.Modules.DefenseCouncil.Domain.Entities.CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = assignment.Id, CouncilMemberId = member.Id, Score = score, EvaluatedAt = archiveTime.AddDays(-1) };
                defense.CouncilScores.Add(row); defense.EvaluationDetails.Add(new UGTM.Modules.DefenseCouncil.Domain.Entities.EvaluationDetail { Id = Guid.NewGuid(), EvaluationId = row.Id, CriteriaId = _scoreCriteriaId, Score = score });
            }
            await defense.SaveChangesAsync();
        }
        using var submitted = await ArchiveAsync(student: student); var current = await ArchivalDataAsync(submitted);
        using var advisorConfirm = await ConfirmArchivalAsync(current); var reviewed = await ArchivalDataAsync(advisorConfirm);
        using var facultyConfirm = await ConfirmArchivalAsync(reviewed, "FacultyConfirm", _adminId, "Admin"); await ArchivalDataAsync(facultyConfirm);
        using var secondCalculation = await CalculateAsync(); var second = await ArchivalDataAsync(secondCalculation);
        var selections = new[] { new { groupId = firstGroup, expectedSummaryVersion = firstVersion }, new { groupId = group, expectedSummaryVersion = second.GetProperty("summaryVersion").GetUInt32() } };
        var staleSelections = new[] { selections[0], new { groupId = group, expectedSummaryVersion = selections[1].expectedSummaryVersion + 1 } };
        using (var rejected = await PublishScoresAsync(new { batchId = _batchId, selections = staleSelections })) Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        await using (var check = Db<DefenseCouncilDbContext>()) Assert.Equal(2, await check.ScoreSummarys.CountAsync(s => s.Status == ScoreSummaryStatus.Calculated));
        var key = Guid.NewGuid(); using var published = await PublishScoresAsync(new { batchId = _batchId, selections }, key); var data = await ArchivalDataAsync(published);
        Assert.Equal(2, data.GetProperty("publishedCount").GetInt32());
        using var replay = await PublishScoresAsync(new { batchId = _batchId, selections = selections.Reverse().ToArray() }, key);
        Assert.Equal(await published.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var own = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={group}", student, "Student");
        Assert.Equal("Second thesis", (await ArchivalDataAsync(own)).GetProperty("topicTitle").GetString());
        using var other = await SendAsync(HttpMethod.Get, $"{ScoresRoute}/my-group?groupId={firstGroup}", student, "Student"); Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        await using var verify = Db<DefenseCouncilDbContext>(); Assert.Equal(2, await verify.ScoreSummarys.CountAsync(s => s.Status == ScoreSummaryStatus.PublishedToStudent));
    }
}
