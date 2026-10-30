using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    private async Task<ReviewerAssignment> CreateForCancellationAsync(Guid? key = null)
    {
        using var response = await PostAsync(key: key);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        await using var db = Db<DefenseCouncilDbContext>();
        return await db.ReviewerAssignments.AsNoTracking().SingleAsync(value => value.IsActive);
    }

    private HttpRequestMessage CancelRequest(Guid id, object body, Guid key, string role = "Admin",
        string? permission = null, Guid? actor = null)
    {
        var request = Request(body, key, role, permission, actor);
        request.Method = HttpMethod.Delete;
        request.RequestUri = new Uri($"{CreationRoute}/{id}", UriKind.Relative);
        return request;
    }

    private async Task<HttpResponseMessage> DeleteAsync(Guid id, uint version, string? reason = "Điều chỉnh phân công",
        Guid? key = null, string role = "Admin", string? permission = null, Guid? actor = null)
    {
        var client = await HttpAsync();
        using var request = CancelRequest(id, new { expectedAssignmentVersion = version, reason },
            key ?? Guid.NewGuid(), role, permission, actor);
        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Faculty")]
    [InlineData("AcademicAffairs")]
    public async Task Cancellation_http_commits_metadata_new_version_and_actual_actor_audit_while_preserving_history(string role)
    {
        var original = await CreateForCancellationAsync();
        var before = DateTime.UtcNow;
        using var response = await DeleteAsync(original.Id, original.Version, "  Lý do riêng tư của Khoa.  ",
            role: role, permission: role == "Admin" ? null : PermissionCodes.ThesisReviewerAssignmentsManage, actor: _advisor);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var dto = json.RootElement.GetProperty("data");
        Assert.False(dto.GetProperty("isActive").GetBoolean());
        Assert.Equal(_advisor, dto.GetProperty("cancelledBy").GetGuid());
        Assert.Equal("Lý do riêng tư của Khoa.", dto.GetProperty("cancellationReason").GetString());
        Assert.InRange(dto.GetProperty("cancelledAtUtc").GetDateTime(), before, DateTime.UtcNow);
        Assert.True(dto.GetProperty("assignmentVersion").GetUInt32() != original.Version);
        await using var db = Db<DefenseCouncilDbContext>();
        var cancelled = await db.ReviewerAssignments.SingleAsync();
        Assert.Equal(original.AssignedBy, cancelled.AssignedBy);
        Assert.Equal(original.AssignedAt, cancelled.AssignedAt);
        Assert.Equal(original.AdvisorEvaluationId, cancelled.AdvisorEvaluationId);
        Assert.Equal(original.AdvisorIdAtAssignment, cancelled.AdvisorIdAtAssignment);
        Assert.Equal(original.FinalReportId, cancelled.FinalReportId);
        Assert.Equal(original.ReportVersionNo, cancelled.ReportVersionNo);
        Assert.Equal(original.SubmissionFileId, cancelled.SubmissionFileId);
        Assert.Equal(original.SubmissionChecksumSha256, cancelled.SubmissionChecksumSha256);
        Assert.Equal(original.ReviewerId, cancelled.ReviewerId);
        Assert.Equal(cancelled.Version, dto.GetProperty("assignmentVersion").GetUInt32());
        var receipt = await db.ReviewerAssignmentIdempotencyRecords.SingleAsync(value => value.Operation == "ReviewerAssignment.Cancel");
        Assert.Equal(200, receipt.ResponseStatusCode);
        Assert.Equal(_advisor, receipt.ActorId);
        var audit = JsonSerializer.Deserialize<AuditIntegrationEvent>((await db.AuditOutboxMessages.SingleAsync(value =>
            value.Action == IntegrationAuditActions.ReviewerAssignmentCancelled)).PayloadJson)!;
        Assert.Equal(_advisor, audit.ActorUserId);
        Assert.Equal(dto.GetProperty("cancelledAtUtc").GetDateTime(), audit.OccurredAtUtc);
        // PostgreSQL persists microseconds; the clock and JSON audit retain .NET's 100 ns ticks.
        Assert.InRange(Math.Abs((cancelled.CancelledAt!.Value - audit.OccurredAtUtc).Ticks), 0L, 9L);
        using var metadata = JsonDocument.Parse(audit.MetadataJson!);
        Assert.True(metadata.RootElement.GetProperty("HasCancellationReason").GetBoolean());
        Assert.DoesNotContain("Lý do riêng tư", audit.MetadataJson);
        Assert.False(metadata.RootElement.TryGetProperty("CancellationReason", out _));
        Assert.False(metadata.RootElement.TryGetProperty("Score", out _));
        await AssertCountsAsync(1, 2, 2);
        await using var thesis = Db<ThesisManagementDbContext>();
        Assert.Equal(UGTM.Modules.ThesisManagement.Domain.Enums.StudentGroupStatus.ReadyForReviewerAssignment,
            (await thesis.StudentGroups.SingleAsync()).Status);
        Assert.Equal(UGTM.Modules.ThesisManagement.Domain.Enums.FinalReportStatus.UnderEvaluation,
            (await thesis.FinalReports.SingleAsync()).Status);
        Assert.True((await db.AdvisorEvaluations.SingleAsync()).IsLocked);
    }

    [Theory]
    [InlineData("Student", true)]
    [InlineData("Lecturer", true)]
    [InlineData("Advisor", true)]
    [InlineData("Reviewer", true)]
    [InlineData("Faculty", false)]
    [InlineData("AcademicAffairs", false)]
    public async Task Cancellation_http_denies_non_management_roles_and_read_only_claims_before_body_or_lookup(string role, bool manage)
    {
        var client = await HttpAsync();
        using var request = CancelRequest(Guid.NewGuid(), new { }, Guid.NewGuid(), role,
            manage ? PermissionCodes.ThesisReviewerAssignmentsManage : PermissionCodes.ThesisReviewerAssignmentsRead);
        request.Content = new StringContent("invalid json", Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertCountsAsync(0, 0, 0);
    }

    [Theory]
    [InlineData(false, "Admin", false, "ReviewerAssignment.AuthenticationRequired")]
    [InlineData(true, "Lecturer", true, "ReviewerAssignment.AccessDenied")]
    [InlineData(true, "Faculty", false, "ReviewerAssignment.AccessDenied")]
    public async Task Cancellation_handler_independently_checks_authorization_before_invalid_input(
        bool authenticated, string role, bool manage, string code)
    {
        var accessor = _app.Services.GetRequiredService<IHttpContextAccessor>();
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, _actor.ToString()), new(ClaimTypes.Role, role)];
        if (manage) claims.Add(new("permission", PermissionCodes.ThesisReviewerAssignmentsManage));
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticated ? "DirectCancellationTest" : null))
        };
        try
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<CancelReviewerAssignmentHandler>()
                .HandleAsync(new(Guid.Empty, 0, null, Guid.Empty));
            Assert.True(result.IsFailure);
            Assert.Equal(code, result.Error.Code);
            await AssertCountsAsync(0, 0, 0);
        }
        finally { accessor.HttpContext = null; }
    }

    [Fact]
    public async Task Cancellation_http_validates_auth_header_route_json_and_uint_version_in_development_without_mutation()
    {
        var original = await CreateForCancellationAsync();
        var client = await HttpAsync();
        using (var anonymous = await client.DeleteAsync($"{CreationRoute}/{original.Id}"))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var header in new[] { "missing", "", "bad", Guid.Empty.ToString(), "multiple" })
        {
            using var request = CancelRequest(original.Id, new { expectedAssignmentVersion = original.Version, reason = "Sửa sai" }, Guid.NewGuid());
            request.Headers.Remove("Idempotency-Key");
            if (header != "missing") request.Headers.TryAddWithoutValidation("Idempotency-Key", header == "multiple"
                ? new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() } : new[] { header });
            using var response = await client.SendAsync(request);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest");
        }
        foreach (var body in new[] { "null", "{}", "{", "[]", "{\"reason\":\"Sửa sai\"}",
            "{\"expectedAssignmentVersion\":0,\"reason\":\"Sửa sai\"}",
            "{\"expectedAssignmentVersion\":-1,\"reason\":\"Sửa sai\"}",
            "{\"expectedAssignmentVersion\":4294967296,\"reason\":\"Sửa sai\"}",
            "{\"expectedAssignmentVersion\":1.5,\"reason\":\"Sửa sai\"}",
            $"{{\"expectedAssignmentVersion\":\"{original.Version}\",\"reason\":\"Sửa sai\"}}" })
        {
            using var request = CancelRequest(original.Id, new { }, Guid.NewGuid());
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest");
        }
        using (var emptyId = await DeleteAsync(Guid.Empty, original.Version))
            await AssertErrorAsync(emptyId, HttpStatusCode.BadRequest, "InvalidRequest");
        using (var request = CancelRequest(original.Id, new { }, Guid.NewGuid()))
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "text/plain");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }
        await AssertCountsAsync(1, 1, 1);
        await AssertStillActiveAsync(original);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n\u2003 ")]
    [InlineData("too-long")]
    [InlineData("too-many-runes")]
    public async Task Cancellation_http_rejects_missing_blank_or_overlong_reason(string? reason)
    {
        var original = await CreateForCancellationAsync();
        reason = reason switch { "too-long" => new string('a', 1001), "too-many-runes" => string.Concat(Enumerable.Repeat("😀", 1001)), _ => reason };
        using var response = await DeleteAsync(original.Id, original.Version, reason);
        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "InvalidCancellationReason");
        await AssertCountsAsync(1, 1, 1);
        await AssertStillActiveAsync(original);
    }

    [Fact]
    public async Task Cancellation_http_accepts_one_thousand_unicode_runes_and_only_uses_server_controlled_fields()
    {
        var original = await CreateForCancellationAsync();
        var reason = string.Concat(Enumerable.Repeat("😀", 1000));
        var client = await HttpAsync();
        using var request = CancelRequest(original.Id, new
        {
            expectedAssignmentVersion = original.Version,
            reason = $"  {reason}  ",
            assignedBy = _student,
            cancelledBy = _student,
            cancelledAt = Now.AddYears(-1),
            isActive = true,
            reviewerId = _otherReviewer,
            submissionFileId = _oldFile
        }, Guid.NewGuid());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = Db<DefenseCouncilDbContext>();
        var cancelled = await db.ReviewerAssignments.SingleAsync();
        Assert.Equal(reason, cancelled.CancellationReason);
        Assert.Equal(_actor, cancelled.CancelledBy);
        Assert.Equal(original.AssignedBy, cancelled.AssignedBy);
        Assert.Equal(original.AssignedAt, cancelled.AssignedAt);
        Assert.Equal(original.ReviewerId, cancelled.ReviewerId);
        Assert.Equal(original.SubmissionFileId, cancelled.SubmissionFileId);
        Assert.False(cancelled.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_http_blocks_any_evaluation_even_zero_ignored_hidden_or_not_approved_and_prioritizes_version(bool ignored)
    {
        var original = await CreateForCancellationAsync();
        await using var db = Db<DefenseCouncilDbContext>();
        var sheet = new ReviewerEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = original.Id,
            Score = 0m,
            IsIgnoredInCalculation = ignored,
            ExclusionReason = ignored ? "Không tính điểm" : null,
            IsVisibleToStudent = false,
            DefenseApproval = DefenseApprovalStatus.Rejected,
            EvaluatedAt = Now
        };
        db.ReviewerEvaluations.Add(sheet);
        await db.SaveChangesAsync();
        using (var stale = await DeleteAsync(original.Id, original.Version + 1))
            await AssertErrorAsync(stale, HttpStatusCode.Conflict, "VersionConflict");
        using (var exists = await DeleteAsync(original.Id, original.Version))
            await AssertErrorAsync(exists, HttpStatusCode.Conflict, "EvaluationExists");
        Assert.Equal(sheet.Id, (await db.ReviewerEvaluations.SingleAsync()).Id);
        Assert.Equal(0m, (await db.ReviewerEvaluations.SingleAsync()).Score);
        await AssertStillActiveAsync(original);
        await AssertCountsAsync(1, 1, 1);
    }

    [Theory]
    [InlineData("batch")]
    [InlineData("group")]
    [InlineData("account")]
    [InlineData("role")]
    [InlineData("binding")]
    [InlineData("advisor")]
    public async Task Cancellation_http_remains_available_after_creation_guards_become_invalid(string change)
    {
        var original = await CreateForCancellationAsync();
        await using (var writer = Db<UGTMDbContext>()) await writer.Database.ExecuteSqlInterpolatedAsync(Change(change));
        using var response = await DeleteAsync(original.Id, original.Version);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await AssertCountsAsync(1, 2, 2);
        await using var db = Db<DefenseCouncilDbContext>();
        Assert.False((await db.ReviewerAssignments.SingleAsync()).IsActive);
        await using var thesis = Db<ThesisManagementDbContext>();
        Assert.Equal(change == "group" ? UGTM.Modules.ThesisManagement.Domain.Enums.StudentGroupStatus.Stopped
            : UGTM.Modules.ThesisManagement.Domain.Enums.StudentGroupStatus.ReadyForReviewerAssignment,
            (await thesis.StudentGroups.SingleAsync()).Status);
    }

    [Fact]
    public async Task Cancellation_http_handles_missing_stale_and_already_cancelled_assignments_in_contract_order()
    {
        var original = await CreateForCancellationAsync();
        using (var missing = await DeleteAsync(Guid.NewGuid(), original.Version))
            await AssertErrorAsync(missing, HttpStatusCode.NotFound, "NotFound");
        using (var stale = await DeleteAsync(original.Id, original.Version + 1))
            await AssertErrorAsync(stale, HttpStatusCode.Conflict, "VersionConflict");
        using (var cancelled = await DeleteAsync(original.Id, original.Version)) Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using (var repeat = await DeleteAsync(original.Id, original.Version))
            await AssertErrorAsync(repeat, HttpStatusCode.Conflict, "AlreadyCancelled");
        await AssertCountsAsync(1, 2, 2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_http_allows_new_assignment_for_same_or_other_reviewer_and_old_replays_do_not_change_it(bool sameReviewer)
    {
        var createKey = Guid.NewGuid();
        var createBody = await BodyAsync();
        var original = await CreateForCancellationAsync(createKey);
        var cancelKey = Guid.NewGuid();
        using var cancelled = await DeleteAsync(original.Id, original.Version, "  Lý do  ", cancelKey);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var receipt = await cancelled.Content.ReadAsStringAsync();
        using var newAssignment = await PostAsync(await BodyAsync(sameReviewer ? _reviewer : _otherReviewer));
        Assert.Equal(HttpStatusCode.Created, newAssignment.StatusCode);
        using var cancelReplay = await DeleteAsync(original.Id, original.Version, "Lý do", cancelKey);
        Assert.Equal(HttpStatusCode.OK, cancelReplay.StatusCode);
        Assert.Equal(receipt, await cancelReplay.Content.ReadAsStringAsync());
        using var createReplay = await PostAsync(createBody, createKey);
        Assert.Equal(HttpStatusCode.Created, createReplay.StatusCode);
        using var replayJson = JsonDocument.Parse(await createReplay.Content.ReadAsStringAsync());
        Assert.Equal(original.Id, replayJson.RootElement.GetProperty("data").GetProperty("id").GetGuid());
        Assert.True(replayJson.RootElement.GetProperty("data").GetProperty("isActive").GetBoolean());
        await using var db = Db<DefenseCouncilDbContext>();
        var current = await db.ReviewerAssignments.SingleAsync(value => value.IsActive);
        Assert.NotEqual(original.Id, current.Id);
        Assert.Equal(sameReviewer ? _reviewer : _otherReviewer, current.ReviewerId);
        Assert.Equal(1, current.AssignmentOrder);
        Assert.False((await db.ReviewerAssignments.SingleAsync(value => value.Id == original.Id)).IsActive);
        using var reusedRoute = await DeleteAsync(current.Id, current.Version, "Lý do", cancelKey);
        await AssertErrorAsync(reusedRoute, HttpStatusCode.Conflict, "IdempotencyKeyReused");
        using var reusedReason = await DeleteAsync(original.Id, original.Version, "Lý do khác", cancelKey);
        await AssertErrorAsync(reusedReason, HttpStatusCode.Conflict, "IdempotencyKeyReused");
        using var reusedVersion = await DeleteAsync(original.Id, original.Version + 1, "Lý do", cancelKey);
        await AssertErrorAsync(reusedVersion, HttpStatusCode.Conflict, "IdempotencyKeyReused");
        using var otherActor = await DeleteAsync(original.Id, original.Version, "Lý do", cancelKey, actor: _advisor);
        await AssertErrorAsync(otherActor, HttpStatusCode.Conflict, "AlreadyCancelled");
        using var forbidden = await DeleteAsync(original.Id, original.Version, "Lý do", cancelKey, "Faculty", PermissionCodes.ThesisReviewerAssignmentsRead);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await AssertCountsAsync(2, 3, 3);
    }

    [Fact]
    public async Task Cancellation_http_uses_separate_operation_scope_and_recovers_after_losing_the_response()
    {
        var key = Guid.NewGuid();
        var original = await CreateForCancellationAsync(key);
        var client = await HttpAsync();
        using var request = CancelRequest(original.Id, new { expectedAssignmentVersion = original.Version, reason = "Sửa sai" }, key);
        request.Headers.Add("X-Test-Discard-Response", "true");
        using var lost = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, lost.StatusCode);
        await AssertCountsAsync(1, 2, 2);
        using var replay = await DeleteAsync(original.Id, original.Version, "Sửa sai", key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        await AssertCountsAsync(1, 2, 2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_http_concurrent_same_or_different_keys_cancel_once(bool sameKey)
    {
        var original = await CreateForCancellationAsync();
        var key = Guid.NewGuid();
        var responses = await Task.WhenAll(DeleteAsync(original.Id, original.Version, key: key),
            DeleteAsync(original.Id, original.Version, key: sameKey ? key : Guid.NewGuid()));
        try
        {
            Assert.Contains(responses, value => value.StatusCode == HttpStatusCode.OK);
            if (sameKey)
            {
                Assert.All(responses, value => Assert.Equal(HttpStatusCode.OK, value.StatusCode));
                Assert.Equal(await responses[0].Content.ReadAsStringAsync(), await responses[1].Content.ReadAsStringAsync());
            }
            else await AssertErrorAsync(responses.Single(value => value.StatusCode != HttpStatusCode.OK), HttpStatusCode.Conflict, "AlreadyCancelled");
            await AssertCountsAsync(1, 2, 2);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_http_rolls_back_on_either_save_failure_and_same_key_retry_can_succeed(bool secondSave)
    {
        var original = await CreateForCancellationAsync();
        var key = Guid.NewGuid();
        await using var db = Db<DefenseCouncilDbContext>();
        var sql = secondSave ? """
            ALTER TABLE councils.reviewer_assignment_idempotency ADD CONSTRAINT cp4_force_failure
            CHECK (operation <> 'ReviewerAssignment.Cancel' OR response_payload_json = '{{}}'::jsonb)
            """ : """
            ALTER TABLE councils.audit_outbox_message ADD CONSTRAINT cp4_force_failure
            CHECK (action <> 'DefenseCouncil.ReviewerAssignment.Cancelled')
            """;
        await db.Database.ExecuteSqlRawAsync(sql);
        using var failed = await DeleteAsync(original.Id, original.Version, key: key);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.DoesNotContain("cp4_force_failure", await failed.Content.ReadAsStringAsync());
        await AssertStillActiveAsync(original);
        await AssertCountsAsync(1, 1, 1);
        await db.Database.ExecuteSqlRawAsync(secondSave
            ? "ALTER TABLE councils.reviewer_assignment_idempotency DROP CONSTRAINT cp4_force_failure"
            : "ALTER TABLE councils.audit_outbox_message DROP CONSTRAINT cp4_force_failure");
        using var retry = await DeleteAsync(original.Id, original.Version, key: key);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        await AssertCountsAsync(1, 2, 2);
    }

    [Fact]
    public async Task Cancellation_http_preserves_legacy_null_binding_and_order_without_inventing_a_snapshot()
    {
        var id = Guid.NewGuid();
        await using var db = Db<DefenseCouncilDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO councils.reviewer_assignment (id, group_id, reviewer_id, assigned_by, assigned_at, assignment_order, is_active)
            VALUES ({id}, {_group}, {_reviewer}, {_actor}, {Now.AddDays(-1)}, 2, true)
            """);
        var original = await db.ReviewerAssignments.AsNoTracking().SingleAsync();
        using var response = await DeleteAsync(id, original.Version);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var dto = json.RootElement.GetProperty("data");
        Assert.True(dto.GetProperty("isLegacy").GetBoolean());
        Assert.Equal(2, dto.GetProperty("assignmentOrder").GetInt32());
        foreach (var field in new[] { "advisorIdAtAssignment", "advisorEvaluationId", "finalReportId", "reportVersionNo", "submissionFileId", "submissionChecksumSha256" })
            Assert.Equal(JsonValueKind.Null, dto.GetProperty(field).ValueKind);
        await AssertCountsAsync(1, 1, 1);
    }

    private async Task AssertStillActiveAsync(ReviewerAssignment original)
    {
        await using var db = Db<DefenseCouncilDbContext>();
        var current = await db.ReviewerAssignments.SingleAsync(value => value.Id == original.Id);
        Assert.True(current.IsActive);
        Assert.Equal(original.Version, current.Version);
        Assert.Null(current.CancelledBy);
        Assert.Null(current.CancelledAt);
        Assert.Null(current.CancellationReason);
    }
}
