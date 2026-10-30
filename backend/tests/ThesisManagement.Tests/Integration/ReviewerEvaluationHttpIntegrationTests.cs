using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Database.Migrations;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    private const string EvaluationRoute = "/api/thesis/reviewer-evaluations";
    private static Dictionary<string, object?> EvaluationBody(Guid id, uint assignmentVersion, uint? evaluationVersion = null,
        string action = "SaveDraft", decimal? score = null, string? comment = null, string? decision = null)
        => new()
        {
            ["assignmentId"] = id,
            ["expectedAssignmentVersion"] = assignmentVersion,
            ["expectedEvaluationVersion"] = evaluationVersion,
            ["action"] = action,
            ["score"] = score,
            ["comment"] = comment,
            ["defenseApproval"] = decision
        };

    private HttpRequestMessage EvaluationRequest(object body, Guid key, string role = "Admin", Guid? actor = null, string? permission = null)
    {
        var request = Request(body, key, role, permission, actor);
        request.RequestUri = new Uri(EvaluationRoute, UriKind.Relative);
        return request;
    }
    private async Task<HttpResponseMessage> SaveEvaluationAsync(object body, Guid? key = null, string role = "Admin", Guid? actor = null)
    {
        using var request = EvaluationRequest(body, key ?? Guid.NewGuid(), role, actor);
        return await (await HttpAsync()).SendAsync(request);
    }
    private async Task<HttpResponseMessage> ReadEvaluationAsync(Guid id, string role = "Admin", Guid? actor = null, string? permission = null, string query = "")
    {
        using var request = EvaluationRequest(new { }, Guid.NewGuid(), role, actor, permission);
        request.Method = HttpMethod.Get; request.Content = null;
        request.RequestUri = new Uri($"{CreationRoute}/{id}/evaluation{query}", UriKind.Relative);
        return await (await HttpAsync()).SendAsync(request);
    }
    private static async Task<JsonElement> EvaluationDataAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        using var json = JsonDocument.Parse(payload); return json.RootElement.GetProperty("data").Clone();
    }
    private static async Task EvaluationErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status}; got {response.StatusCode}: {payload}");
        using var json = JsonDocument.Parse(payload);
        Assert.Equal($"ReviewerEvaluation.{code}", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
    private async Task AssertEvaluationCountsAsync(int rows, int receipts, int audits)
    {
        await using var db = Db<DefenseCouncilDbContext>();
        Assert.Equal(rows, await db.ReviewerEvaluations.CountAsync());
        Assert.Equal(receipts, await db.ReviewerEvaluationIdempotencyRecords.CountAsync());
        Assert.Equal(audits, await db.AuditOutboxMessages.CountAsync(value => value.Action == IntegrationAuditActions.ReviewerEvaluationDraftSaved
            || value.Action == IntegrationAuditActions.ReviewerEvaluationSubmitted));
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public async Task Evaluation_cp2_draft_replace_submit_locks_both_decisions_and_audits_actual_actor_with_saved_version(string decision)
    {
        var parent = await CreateForCancellationAsync();
        using var draft = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version), role: "Lecturer", actor: _reviewer);
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var first = await EvaluationDataAsync(draft);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("score").ValueKind);
        Assert.False(first.GetProperty("isVisibleToStudent").GetBoolean());
        Assert.Equal(_reviewer, first.GetProperty("createdBy").GetGuid());
        using var changed = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, first.GetProperty("evaluationVersion").GetUInt32(),
            score: 0, comment: "  Góp ý nháp  ", decision: "Rejected"), role: "Lecturer", actor: _reviewer);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var second = await EvaluationDataAsync(changed);
        Assert.Equal(0m, second.GetProperty("score").GetDecimal());
        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        using var replaced = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, second.GetProperty("evaluationVersion").GetUInt32(), comment: "Góp ý thay thế"));
        var third = await EvaluationDataAsync(replaced);
        Assert.Equal(JsonValueKind.Null, third.GetProperty("score").ValueKind);
        Assert.Equal(JsonValueKind.Null, third.GetProperty("defenseApproval").ValueKind);
        using var submitted = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, third.GetProperty("evaluationVersion").GetUInt32(),
            "Submit", 8.345m, "  Góp ý đã công khai  ", decision));
        var final = await EvaluationDataAsync(submitted);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Equal(8.35m, final.GetProperty("score").GetDecimal());
        Assert.Equal("Góp ý đã công khai", final.GetProperty("comment").GetString());
        Assert.Equal("Submitted", final.GetProperty("status").GetString());
        Assert.Equal(decision, final.GetProperty("defenseApproval").GetString());
        Assert.True(final.GetProperty("isLocked").GetBoolean()); Assert.True(final.GetProperty("isVisibleToStudent").GetBoolean());
        Assert.Equal(_actor, final.GetProperty("updatedBy").GetGuid()); Assert.Equal(_actor, final.GetProperty("submittedBy").GetGuid());
        Assert.Equal(_reviewer, final.GetProperty("createdBy").GetGuid());
        Assert.Equal(parent.Version, final.GetProperty("assignmentVersion").GetUInt32());
        foreach (var (role, actor) in new[] { ("Admin", _actor), ("Lecturer", _reviewer) })
        {
            using var locked = await SaveEvaluationAsync(EvaluationBody(parent.Id, 1, null, "Submit", 9, "Khác", "Approved"), role: role, actor: actor);
            await EvaluationErrorAsync(locked, HttpStatusCode.Conflict, "Locked");
        }
        using var cancel = await DeleteAsync(parent.Id, parent.Version);
        await AssertErrorAsync(cancel, HttpStatusCode.Conflict, "EvaluationExists");
        using var read = await ReadEvaluationAsync(parent.Id, "Lecturer", _reviewer);
        var readDto = await EvaluationDataAsync(read);
        Assert.Equal(final.GetProperty("evaluationVersion").GetUInt32(), readDto.GetProperty("evaluationVersion").GetUInt32());
        await AssertEvaluationCountsAsync(1, 4, 4);
        await using var db = Db<DefenseCouncilDbContext>();
        var receipt = (await db.ReviewerEvaluationIdempotencyRecords.ToListAsync())
            .Single(value => JsonSerializer.Deserialize<ReviewerEvaluationDto>(value.ResponsePayloadJson)!.Status == "Submitted");
        using var receiptJson = JsonDocument.Parse(receipt.ResponsePayloadJson);
        Assert.Equal(final.GetProperty("evaluationVersion").GetUInt32(), receiptJson.RootElement.GetProperty("EvaluationVersion").GetUInt32());
        var audit = JsonSerializer.Deserialize<AuditIntegrationEvent>((await db.AuditOutboxMessages.SingleAsync(value => value.Action == IntegrationAuditActions.ReviewerEvaluationSubmitted)).PayloadJson)!;
        Assert.Equal(_actor, audit.ActorUserId);
        using var metadata = JsonDocument.Parse(audit.MetadataJson!);
        Assert.Equal(final.GetProperty("evaluationVersion").GetUInt32(), metadata.RootElement.GetProperty("EvaluationVersion").GetUInt32());
        Assert.DoesNotContain("Góp ý", audit.MetadataJson); Assert.DoesNotContain("Score", audit.MetadataJson);
        foreach (var excluded in new[] { "advisorScore", "advisorComment", "objectKey", "downloadUrl", "canProceed", "revisionFeedback", "exclusionReason" })
            Assert.False(final.TryGetProperty(excluded, out _));
    }

    [Theory]
    [InlineData("Approved", 0)]
    [InlineData("Rejected", 10)]
    public async Task Evaluation_cp2_direct_submit_does_not_require_reviewer_threshold_or_student_window(string decision, int score)
    {
        var parent = await CreateForCancellationAsync(); // Fixture has no Reviewer threshold and an expired student window.
        using var response = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, action: "Submit", score: score, comment: "Kết luận phản biện", decision: decision),
            role: "Lecturer", actor: _reviewer);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await EvaluationDataAsync(response); Assert.Equal("Submitted", data.GetProperty("status").GetString());
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Evaluation_cp2_write_role_and_ownership_checks_precede_body_lookup_and_receipt_disclosure()
    {
        var client = await HttpAsync();
        foreach (var role in new[] { "Student", "Faculty", "AcademicAffairs", "Reviewer", "Advisor", "CouncilMember" })
        {
            using var request = EvaluationRequest(new { }, Guid.NewGuid(), role, permission: PermissionCodes.ThesisReviewerEvaluationsRead);
            request.Content = new StringContent("invalid JSON", Encoding.UTF8, "text/plain");
            using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using (var anonymous = await client.PostAsync(EvaluationRoute, new StringContent("invalid"))) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var parent = await CreateForCancellationAsync(); var key = Guid.NewGuid();
        var body = EvaluationBody(parent.Id, parent.Version, action: "Submit", score: 8, comment: "Riêng tư", decision: "Approved");
        using var saved = await SaveEvaluationAsync(body, key);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        foreach (var actor in new[] { _advisor, _otherReviewer })
        {
            using var forbidden = await SaveEvaluationAsync(body, key, "Lecturer", actor);
            await EvaluationErrorAsync(forbidden, HttpStatusCode.Forbidden, "AccessDenied");
        }
        await using (var db = Db<UGTMDbContext>()) await db.Database.ExecuteSqlInterpolatedAsync(Change("account"));
        using var inactive = await SaveEvaluationAsync(body, key, "Lecturer", _reviewer);
        await EvaluationErrorAsync(inactive, HttpStatusCode.Forbidden, "AccessDenied");
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Evaluation_cp2_private_read_role_matrix_is_side_effect_free_and_retains_history_access()
    {
        var parent = await CreateForCancellationAsync();
        using var saved = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, action: "Submit", score: 7, comment: "Góp ý", decision: "Approved"));
        var version = (await EvaluationDataAsync(saved)).GetProperty("evaluationVersion").GetUInt32();
        foreach (var (role, actor, permission, allowed) in new[] {
            ("Admin", _actor, (string?)null, true), ("Faculty", _actor, PermissionCodes.ThesisReviewerEvaluationsRead, true),
            ("AcademicAffairs", _actor, PermissionCodes.ThesisReviewerEvaluationsRead, true), ("Lecturer", _reviewer, null, true),
            ("Lecturer", _otherReviewer, PermissionCodes.ThesisReviewerEvaluationsRead, false), ("Lecturer", _advisor, null, false),
            ("Student", _student, PermissionCodes.ThesisReviewerEvaluationsRead, false), ("Faculty", _actor, null, false),
            ("AcademicAffairs", _actor, PermissionCodes.ThesisReviewerAssignmentsManage, false) })
        {
            using var read = await ReadEvaluationAsync(parent.Id, role, actor, permission);
            Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, read.StatusCode);
        }
        await using (var db = Db<UGTMDbContext>())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(Change("batch"));
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.student_group SET status='Defended' WHERE id={_group}");
            await db.Database.ExecuteSqlInterpolatedAsync(Change("advisor"));
        }
        using (var history = await ReadEvaluationAsync(parent.Id, "Lecturer", _reviewer))
            Assert.Equal(version, (await EvaluationDataAsync(history)).GetProperty("evaluationVersion").GetUInt32());
        await using (var db = Db<UGTMDbContext>()) await db.Database.ExecuteSqlInterpolatedAsync(Change("account"));
        using (var inactive = await ReadEvaluationAsync(parent.Id, "Lecturer", _reviewer)) await EvaluationErrorAsync(inactive, HttpStatusCode.Forbidden, "AccessDenied");
        using (var admin = await ReadEvaluationAsync(parent.Id)) Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        using (var faculty = await ReadEvaluationAsync(parent.Id, "Faculty", permission: PermissionCodes.ThesisReviewerEvaluationsRead)) Assert.Equal(HttpStatusCode.OK, faculty.StatusCode);
        using (var query = await ReadEvaluationAsync(parent.Id, query: "?includeScores=true")) await EvaluationErrorAsync(query, HttpStatusCode.BadRequest, "InvalidRequest");
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Evaluation_cp2_missing_legacy_and_context_errors_do_not_create_a_draft()
    {
        using (var absent = await ReadEvaluationAsync(Guid.NewGuid())) await EvaluationErrorAsync(absent, HttpStatusCode.NotFound, "AssignmentNotFound");
        var parent = await CreateForCancellationAsync();
        using (var empty = await ReadEvaluationAsync(parent.Id)) await EvaluationErrorAsync(empty, HttpStatusCode.NotFound, "NotFound");
        await AssertEvaluationCountsAsync(0, 0, 0);
        using var saved = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version));
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        await using (var db = Db<UGTMDbContext>()) await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_evaluation SET is_legacy=true");
        using (var legacy = await ReadEvaluationAsync(parent.Id)) await EvaluationErrorAsync(legacy, HttpStatusCode.Conflict, "LegacyRecordNeedsResolution");
        using (var legacy = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version))) await EvaluationErrorAsync(legacy, HttpStatusCode.Conflict, "LegacyRecordNeedsResolution");
        await using (var db = Db<UGTMDbContext>())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_evaluation SET is_legacy=false");
            await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_assignment SET is_legacy=true");
        }
        using (var legacy = await ReadEvaluationAsync(parent.Id)) await EvaluationErrorAsync(legacy, HttpStatusCode.Conflict, "LegacyRecordNeedsResolution");
        using (var legacy = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version))) await EvaluationErrorAsync(legacy, HttpStatusCode.Conflict, "LegacyRecordNeedsResolution");
        await using (var db = Db<UGTMDbContext>())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE councils.reviewer_assignment SET is_legacy=false");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM thesis.student_group_member WHERE group_id={_group}");
        }
        using (var context = await ReadEvaluationAsync(parent.Id)) await EvaluationErrorAsync(context, HttpStatusCode.Conflict, "ContextInvalid");
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Evaluation_cp2_strict_http_parsing_rejects_invalid_headers_json_selectors_and_client_metadata()
    {
        var parent = await CreateForCancellationAsync(); var client = await HttpAsync();
        var body = EvaluationBody(parent.Id, parent.Version); var valid = JsonSerializer.Serialize(body);
        var malformed = new[] { "null", "[]", "{", "{}", valid + "{}",
            valid.Replace("\"SaveDraft\"", "0"), valid.Replace("\"SaveDraft\"", "\"0\""), valid.Replace("\"SaveDraft\"", "\"Other\""),
            valid.Replace("\"score\":null", "\"score\":\"8\""), valid.Replace("\"score\":null", "\"score\":1e100"),
            valid.Replace("\"comment\":null", "\"comment\":{}"), valid.Replace("\"comment\":null", "\"comment\":\"\\ud800\""),
            valid.Replace("\"defenseApproval\":null", "\"defenseApproval\":0"),
            valid.Replace("\"defenseApproval\":null", "\"defenseApproval\":\"0\""), valid.Replace("\"defenseApproval\":null", "\"defenseApproval\":\"Other\""),
            valid.Replace($"\"expectedAssignmentVersion\":{parent.Version}", "\"expectedAssignmentVersion\":\"1\""),
            valid.Replace($"\"expectedAssignmentVersion\":{parent.Version}", "\"expectedAssignmentVersion\":1.0"),
            valid.Replace($"\"expectedAssignmentVersion\":{parent.Version}", "\"expectedAssignmentVersion\":0"),
            valid.Replace($"\"expectedAssignmentVersion\":{parent.Version}", "\"expectedAssignmentVersion\":-1"),
            valid.Replace("\"expectedEvaluationVersion\":null", "\"expectedEvaluationVersion\":4294967296"),
            valid.Replace("\"expectedEvaluationVersion\":null", "\"expectedEvaluationVersion\":0"),
            valid.Replace(parent.Id.ToString(), Guid.Empty.ToString()), valid.Replace(parent.Id.ToString(), "bad-guid"),
            valid[..^1] + ",\"ACTION\":\"Submit\"}", valid[..^1] + ",\"reviewerId\":null}",
            valid[..^1] + ",\"isVisibleToStudent\":true}", valid[..^1] + ",\"isIgnoredInCalculation\":true}",
            valid[..^1] + ",\"submittedBy\":null}", valid[..^1] + ",\"revisionFeedback\":\"Hidden\"}" };
        foreach (var raw in malformed)
        {
            using var request = EvaluationRequest(body, Guid.NewGuid()); request.Content = new StringContent(raw, Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request); await EvaluationErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest");
        }
        foreach (var header in new[] { "missing", "", "bad", Guid.Empty.ToString(), "multiple" })
        {
            using var request = EvaluationRequest(body, Guid.NewGuid()); request.Headers.Remove("Idempotency-Key");
            if (header != "missing") request.Headers.TryAddWithoutValidation("Idempotency-Key", header == "multiple"
                ? new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() } : new[] { header });
            using var response = await client.SendAsync(request); await EvaluationErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest");
        }
        using (var request = EvaluationRequest(body, Guid.NewGuid()))
        {
            request.Content = new StringContent(valid, Encoding.UTF8, "text/plain");
            using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }
        using (var request = EvaluationRequest(body, Guid.NewGuid()))
        {
            request.RequestUri = new Uri(EvaluationRoute + "?reviewerId=" + _reviewer, UriKind.Relative);
            using var response = await client.SendAsync(request); await EvaluationErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest");
        }
        using (var route = await ReadEvaluationAsync(Guid.Empty)) await EvaluationErrorAsync(route, HttpStatusCode.BadRequest, "InvalidRequest");
        await AssertEvaluationCountsAsync(0, 0, 0);
    }

    [Fact]
    public async Task Evaluation_cp2_validates_raw_score_unicode_and_incomplete_submit_before_receipts_or_mutation()
    {
        var parent = await CreateForCancellationAsync(); var key = Guid.NewGuid();
        foreach (var (score, comment, decision, action, code) in new (decimal?, string?, string?, string, string)[] {
            (-0.001m, null, null, "SaveDraft", "InvalidScore"), (10.004m, null, null, "SaveDraft", "InvalidScore"),
            (8, new string('a', 2001), null, "SaveDraft", "InvalidComment"), (8, "x\0x", null, "SaveDraft", "InvalidComment"),
            (null, "Góp ý", "Approved", "Submit", "IncompleteEvaluation"), (8, "\u2003 \t", "Approved", "Submit", "IncompleteEvaluation"),
            (8, "Góp ý", null, "Submit", "IncompleteEvaluation") })
        {
            using var response = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, action: action, score: score, comment: comment, decision: decision), key);
            await EvaluationErrorAsync(response, HttpStatusCode.BadRequest, code);
        }
        await AssertEvaluationCountsAsync(0, 0, 0);
        var unicode = string.Concat(Enumerable.Repeat("😀", 2000));
        using var valid = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, comment: "\u2003" + unicode + "\u2003"), key);
        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
        var data = await EvaluationDataAsync(valid); Assert.Equal(unicode, data.GetProperty("comment").GetString());
        using var tooLong = await SaveEvaluationAsync(EvaluationBody(parent.Id, parent.Version, data.GetProperty("evaluationVersion").GetUInt32(), comment: unicode + "x"));
        await EvaluationErrorAsync(tooLong, HttpStatusCode.BadRequest, "InvalidComment");
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Theory]
    [InlineData(false, "Admin", "ReviewerEvaluation.AuthenticationRequired")]
    [InlineData(true, "Faculty", "ReviewerEvaluation.AccessDenied")]
    [InlineData(true, "Student", "ReviewerEvaluation.AccessDenied")]
    public async Task Evaluation_cp2_handlers_enforce_authorization_independently_of_http(bool authenticated, string role, string code)
    {
        var accessor = _app.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, _actor.ToString()), new Claim(ClaimTypes.Role, role) }, authenticated ? "DirectEvaluationTest" : null))
        };
        try
        {
            await using var scope = _app.Services.CreateAsyncScope();
            Assert.Equal(code, (await scope.ServiceProvider.GetRequiredService<SaveReviewerEvaluationHandler>()
                .HandleAsync(new(Guid.Empty, ReviewerEvaluationAction.SaveDraft, 0, null, null, null, null, Guid.Empty))).Error.Code);
            Assert.Equal(code, (await scope.ServiceProvider.GetRequiredService<GetReviewerEvaluationHandler>().HandleAsync(Guid.Empty)).Error.Code);
        }
        finally { accessor.HttpContext = null; }
        await AssertEvaluationCountsAsync(0, 0, 0);
    }
}
