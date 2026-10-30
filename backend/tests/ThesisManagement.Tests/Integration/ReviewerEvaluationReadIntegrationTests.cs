using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    private readonly Cp3Queries _cp3Queries = new();
    private string FeedbackRoute(Guid? group = null) => $"/api/thesis/groups/{group ?? _group}/feedback";
    private string DefenseGateRoute(Guid? group = null) => $"/api/thesis/groups/{group ?? _group}/defense-eligibility";
    private async Task<ReviewerAssignment> Cp3SubmittedAsync(decimal score = 5.5m, string decision = "Approved", bool publishRule = true)
    {
        var parent = await CreateForCancellationAsync();
        using var submitted = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, action: "Submit", score: score, comment: "Góp ý công khai; KHÔNG kèm điểm", decision: decision));
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        if (publishRule) await PublishFoundationRuleAsync(await SeedFoundationRuleDraftAsync());
        return parent;
    }
    private static async Task<JsonElement> Cp3GateAsync(HttpResponseMessage response, string? reason = null)
    {
        var data = await EvaluationDataAsync(response); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(reason is null, data.GetProperty("canProceed").GetBoolean());
        Assert.Equal(reason, data.GetProperty("reasonCode").GetString());
        Assert.Equal(11, data.EnumerateObject().Count());
        foreach (var field in new[] { "score", "comment", "advisorScore", "reviewerScore", "defenseApproval", "exclusionReason" }) Assert.False(data.TryGetProperty(field, out _));
        return data;
    }
    private static async Task Cp3FeedbackAsync(HttpResponseMessage response, string? expected)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await EvaluationDataAsync(response); var property = Assert.Single(data.EnumerateObject());
        Assert.Equal("comment", property.Name); Assert.Equal(expected, property.Value.GetString());
    }

    [Theory]
    [InlineData("context", "ContextInvalid")]
    [InlineData("group", "GroupNotReady")]
    [InlineData("report", "GroupNotReady")]
    [InlineData("missingAssignment", "AssignmentMissing")]
    [InlineData("legacyAssignment", "LegacyAssignment")]
    [InlineData("advisor", "AdvisorConflict")]
    [InlineData("binding", "BindingInvalid")]
    [InlineData("approval", "BindingInvalid")]
    [InlineData("missingEvaluation", "EvaluationMissing")]
    [InlineData("legacyEvaluation", "LegacyEvaluation")]
    [InlineData("draft", "EvaluationNotSubmitted")]
    [InlineData("excluded", "EvaluationExcluded")]
    public async Task Evaluation_cp3_gate_reports_first_blocking_reason_without_exposing_grade_or_mutating_state(string scenario, string reason)
    {
        ReviewerAssignment? parent = null;
        if (scenario != "missingAssignment") parent = await CreateForCancellationAsync();
        if (parent is not null && scenario != "missingEvaluation")
        {
            using var saved = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version,
                action: scenario == "draft" ? "SaveDraft" : "Submit", score: 5.5m, comment: "Góp ý", decision: "Approved"));
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        }
        await using (var db = Db<UGTMDbContext>())
        {
            switch (scenario)
            {
                case "context": await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM thesis.student_group_member WHERE group_id={_group}"); break;
                case "group": case "advisor": case "binding": await db.Database.ExecuteSqlInterpolatedAsync(Change(scenario)); break;
                case "report": await db.Database.ExecuteSqlRawAsync("UPDATE thesis.final_report SET status='Submitted'"); break;
                case "legacyAssignment": await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_assignment SET is_legacy=true"); break;
                case "legacyEvaluation": await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_evaluation SET is_legacy=true"); break;
                case "approval": await db.Database.ExecuteSqlRawAsync("UPDATE councils.advisor_evaluation SET allows_reviewer_assignment=false, rejection_reason='Chưa đồng ý'"); break;
                case "excluded": await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_evaluation SET is_ignored_in_calculation=true, exclusion_reason='Không công nhận'"); break;
            }
        }
        await using var before = Db<DefenseCouncilDbContext>();
        var audits = await before.AuditOutboxMessages.CountAsync(); var receipts = await before.ReviewerEvaluationIdempotencyRecords.CountAsync();
        var versions = await before.ReviewerEvaluations.AsNoTracking().Select(value => value.Version).ToArrayAsync();
        using var response = await ReadAsync(DefenseGateRoute()); var data = await Cp3GateAsync(response, reason);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("scoreConfigurationId").ValueKind);
        await using var after = Db<DefenseCouncilDbContext>();
        Assert.Equal(audits, await after.AuditOutboxMessages.CountAsync()); Assert.Equal(receipts, await after.ReviewerEvaluationIdempotencyRecords.CountAsync());
        Assert.Equal(versions, await after.ReviewerEvaluations.AsNoTracking().Select(value => value.Version).ToArrayAsync());
    }

    [Theory]
    [InlineData("missing", "ConfigurationMissing")]
    [InlineData("null", "ConfigurationMissing")]
    [InlineData("coverage", "ConfigurationMissing")]
    [InlineData("ambiguous", "ConfigurationAmbiguous")]
    [InlineData("invalid", "ConfigurationInvalid")]
    [InlineData("below", "ScoreBelowMinimum")]
    [InlineData("rejected", "DefenseRejected")]
    [InlineData("equal", null)]
    [InlineData("above", null)]
    [InlineData("zero", null)]
    public async Task Evaluation_cp3_gate_resolves_current_active_reviewer_source_without_default_threshold_or_branch_guessing(string scenario, string? reason)
    {
        await Cp3SubmittedAsync(scenario == "below" ? 5.49m : scenario == "zero" ? 0m : scenario == "above" ? 5.51m : 5.5m,
            scenario == "rejected" ? "Rejected" : "Approved", scenario != "missing");
        await using (var db = Db<UGTMDbContext>())
        {
            switch (scenario)
            {
                case "null": await db.Database.ExecuteSqlRawAsync("UPDATE academic.score_component_config SET minimum_score=NULL WHERE component_code='R_OUT'"); break;
                case "coverage": await db.Database.ExecuteSqlRawAsync("UPDATE academic.score_component_config SET source='Custom' WHERE component_code='R_OUT'"); break;
                case "ambiguous": await db.Database.ExecuteSqlRawAsync("UPDATE academic.score_component_config SET minimum_score=6 WHERE component_code='R_OUT'"); break;
                case "invalid": await db.Database.ExecuteSqlRawAsync("UPDATE academic.score_component_config SET applicability='Unknown' WHERE component_code='R_OUT'"); break;
                case "zero": await db.Database.ExecuteSqlRawAsync("UPDATE academic.score_component_config SET minimum_score=0 WHERE source='Reviewer'"); break;
            }
        }
        using var response = await ReadAsync(DefenseGateRoute()); var data = await Cp3GateAsync(response, reason);
        if (reason is null) Assert.Equal(scenario == "zero" ? 0 : 5.5m, data.GetProperty("minimumReviewerScore").GetDecimal());
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Evaluation_cp3_gate_role_permission_ownership_and_live_account_matrix_is_separate_from_gate_value()
    {
        await Cp3SubmittedAsync();
        foreach (var (role, actor, permission, allowed) in new[] {
            ("Admin", _actor, (string?)null, true), ("Faculty", _actor, PermissionCodes.ThesisReviewerEvaluationsRead, true),
            ("AcademicAffairs", _actor, PermissionCodes.ThesisReviewerEvaluationsRead, true), ("Lecturer", _reviewer, null, true),
            ("Faculty", _actor, null, false), ("AcademicAffairs", _actor, PermissionCodes.ThesisReviewerAssignmentsManage, false),
            ("Lecturer", _otherReviewer, PermissionCodes.ThesisReviewerEvaluationsRead, false), ("Lecturer", _advisor, null, false),
            ("Student", _student, PermissionCodes.ThesisReviewerEvaluationsRead, false) })
        {
            using var response = await ReadAsync(DefenseGateRoute(), actor, role, permission);
            Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        }
        await using (var db = Db<UGTMDbContext>()) { await db.Database.ExecuteSqlInterpolatedAsync(Change("account")); await db.Database.ExecuteSqlInterpolatedAsync(Change("batch")); }
        using (var inactive = await ReadAsync(DefenseGateRoute(), _reviewer, "Lecturer")) await EvaluationErrorAsync(inactive, HttpStatusCode.Forbidden, "AccessDenied");
        using (var admin = await ReadAsync(DefenseGateRoute())) Assert.Equal("Closed", (await Cp3GateAsync(admin)).GetProperty("batchStatus").GetString());
        using (var missing = await ReadAsync(DefenseGateRoute(Guid.NewGuid()))) await EvaluationErrorAsync(missing, HttpStatusCode.NotFound, "GroupNotFound");
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Evaluation_cp3_feedback_requires_student_confirmed_own_group_and_rejects_management_override_or_selectors()
    {
        await Cp3SubmittedAsync(); var client = await HttpAsync();
        foreach (var role in new[] { "Admin", "Faculty", "AcademicAffairs", "Lecturer", "Advisor", "Reviewer", "CouncilMember" })
        {
            using var response = await ReadAsync(FeedbackRoute(), _student, role, PermissionCodes.ThesisReviewerEvaluationsRead);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        foreach (var route in new[] { FeedbackRoute(), DefenseGateRoute() })
        {
            using var anonymous = await client.GetAsync(route); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }
        using (var outside = await ReadAsync(FeedbackRoute(), _otherReviewer, "Student")) await EvaluationErrorAsync(outside, HttpStatusCode.Forbidden, "AccessDenied");
        using (var absent = await ReadAsync(FeedbackRoute(Guid.NewGuid()), _student, "Student")) await EvaluationErrorAsync(absent, HttpStatusCode.NotFound, "GroupNotFound");
        foreach (var query in new[] { "?includeScores=true", "?includeDraft=true", "?reviewerId=" + _reviewer, "?userId=" + _student, "?x=1&x=2" })
        {
            using var feedback = await ReadAsync(FeedbackRoute() + query, _student, "Student"); await EvaluationErrorAsync(feedback, HttpStatusCode.BadRequest, "InvalidRequest");
            using var gate = await ReadAsync(DefenseGateRoute() + query); await EvaluationErrorAsync(gate, HttpStatusCode.BadRequest, "InvalidRequest");
        }
        using (var invalid = await ReadAsync(FeedbackRoute(Guid.Empty), _student, "Student")) await EvaluationErrorAsync(invalid, HttpStatusCode.BadRequest, "InvalidRequest");
        using (var invalid = await ReadAsync(DefenseGateRoute(Guid.Empty))) await EvaluationErrorAsync(invalid, HttpStatusCode.BadRequest, "InvalidRequest");
        await using (var db = Db<UGTMDbContext>()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.student_group_member SET confirmation_status='Pending' WHERE group_id={_group} AND student_id={_student}");
        using (var pending = await ReadAsync(FeedbackRoute(), _student, "Student")) await EvaluationErrorAsync(pending, HttpStatusCode.Forbidden, "AccessDenied");
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Theory]
    [InlineData("none", false)]
    [InlineData("missingEvaluation", false)]
    [InlineData("draft", false)]
    [InlineData("legacyAssignment", false)]
    [InlineData("legacyEvaluation", false)]
    [InlineData("hiddenLegacy", false)]
    [InlineData("approved", true)]
    [InlineData("rejected", true)]
    [InlineData("ignored", true)]
    public async Task Evaluation_cp3_feedback_exposes_only_published_comment_and_never_falls_back_to_revision_feedback(string scenario, bool published)
    {
        if (scenario != "none")
        {
            var parent = await CreateForCancellationAsync();
            if (scenario != "missingEvaluation")
            {
                using var saved = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, action: scenario == "draft" ? "SaveDraft" : "Submit",
                    score: 8.35m, comment: "Góp ý công khai", decision: scenario == "rejected" ? "Rejected" : "Approved"));
                Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
                await using var db = Db<UGTMDbContext>();
                await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_evaluation SET revision_feedback='Không được fallback'");
                if (scenario == "legacyAssignment") await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_assignment SET is_legacy=true");
                if (scenario is "legacyEvaluation" or "hiddenLegacy") await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_evaluation SET is_legacy=true, is_visible_to_student=false");
                if (scenario == "ignored") await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_evaluation SET is_ignored_in_calculation=true, exclusion_reason='Lý do riêng'");
            }
        }
        using var response = await ReadAsync(FeedbackRoute(), _student, "Student"); await Cp3FeedbackAsync(response, published ? "Góp ý công khai" : null);
        var payload = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("Không được fallback", payload); Assert.DoesNotContain("8.35", payload);
    }

    [Theory]
    [InlineData("Defended")]
    [InlineData("ScoreLocked")]
    [InlineData("Completed")]
    public async Task Evaluation_cp3_published_feedback_survives_later_stage_closed_batch_inactive_reviewer_and_new_report_version(string stage)
    {
        await Cp3SubmittedAsync();
        await using (var db = Db<UGTMDbContext>())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(Change("batch")); await db.Database.ExecuteSqlInterpolatedAsync(Change("account"));
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.student_group SET status={stage} WHERE id={_group}");
            await db.Database.ExecuteSqlRawAsync("UPDATE thesis.final_report SET current_version_no=3");
        }
        using var feedback = await ReadAsync(FeedbackRoute(), _student, "Student"); await Cp3FeedbackAsync(feedback, "Góp ý công khai; KHÔNG kèm điểm");
        using var gate = await ReadAsync(DefenseGateRoute()); await Cp3GateAsync(gate, "GroupNotReady");
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Theory]
    [InlineData("binding")]
    [InlineData("approval")]
    [InlineData("context")]
    public async Task Evaluation_cp3_published_feedback_fails_closed_on_corrupt_context_or_snapshot_without_private_errors(string change)
    {
        await Cp3SubmittedAsync();
        await using (var db = Db<UGTMDbContext>())
        {
            if (change == "binding") await db.Database.ExecuteSqlInterpolatedAsync(Change("binding"));
            if (change == "approval") await db.Database.ExecuteSqlRawAsync("UPDATE councils.advisor_evaluation SET submission_checksum_sha256=repeat('b',64)");
            if (change == "context")
            {
                await using var academic = Db<AcademicPeriodsDbContext>();
                var template = await academic.ScheduleTemplateVersions.Include(value => value.StageDefinitions)
                    .SingleAsync(value => value.TemplateCode == "REVIEWER_TRANSACTION");
                var start = new DateOnly(2026, 1, 1);
                var other = KltnBatch.Create(Guid.NewGuid(), "Đợt khác", "2026-2027", 2, start, start.AddDays(146), template, _actor, Now);
                academic.KltnBatches.Add(other); await academic.SaveChangesAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE topics.topic SET batch_id={other.Id} WHERE id={_topic}");
            }
        }
        using var response = await ReadAsync(FeedbackRoute(), _student, "Student");
        await EvaluationErrorAsync(response, HttpStatusCode.Conflict, "ContextInvalid");
        Assert.DoesNotContain("Góp ý", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Evaluation_cp3_feedback_uses_bounded_whitelist_sql_even_with_assignment_history()
    {
        await Cp3SubmittedAsync();
        async Task<int> ReadCountAsync()
        {
            _readCommands.Count = 0; _cp3Queries.Sql.Clear();
            using var response = await ReadAsync(FeedbackRoute(), _student, "Student"); await Cp3FeedbackAsync(response, "Góp ý công khai; KHÔNG kèm điểm");
            Assert.Single(_cp3Queries.Sql);
            foreach (var sql in _cp3Queries.Sql) Assert.DoesNotMatch(@"\b(score|defense_approval|revision_feedback|evaluated_by|exclusion_reason)\b", sql);
            return _readCommands.Count;
        }
        var count = await ReadCountAsync(); Assert.InRange(count, 1, 5);
        await using (var db = Db<DefenseCouncilDbContext>())
        {
            var parent = await db.ReviewerAssignments.AsNoTracking().SingleAsync();
            for (var index = 0; index < 12; index++)
            {
                var old = ReviewerAssignment.Create(Guid.NewGuid(), _group, _reviewer, _advisor, parent.AdvisorEvaluationId!.Value,
                    parent.FinalReportId!.Value, 2, _file, Checksum, _actor, Now);
                Assert.True(old.TryCancel(_actor, Now.AddMinutes(1), "Lịch sử", false)); db.ReviewerAssignments.Add(old);
            }
            await db.SaveChangesAsync();
        }
        Assert.Equal(count, await ReadCountAsync()); await AssertEvaluationCountsAsync(1, 1, 1);
    }

    private sealed class Cp3Queries : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Sql { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Sql.Enqueue(command.CommandText); return ValueTask.FromResult(result); }
    }
}
