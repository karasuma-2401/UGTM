using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

// Reuses the CP2 fixture's real production module registration, FK-valid data and PostgreSQL container.
public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    private const string CreationRoute = "/api/thesis/reviewer-assignments";
    private HttpClient? _httpClient;

    private void ConfigureHttp(WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddScoped<ICurrentUser>(provider =>
        {
            var accessor = provider.GetRequiredService<IHttpContextAccessor>();
            return accessor.HttpContext is null ? new Actor(_actor) : new HttpCurrentUser(accessor);
        });
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CreationAuthentication.Scheme;
            options.DefaultChallengeScheme = CreationAuthentication.Scheme;
            options.DefaultForbidScheme = CreationAuthentication.Scheme;
        }).AddScheme<AuthenticationSchemeOptions, CreationAuthentication>(CreationAuthentication.Scheme, _ => { });
    }

    private async Task<HttpClient> HttpAsync()
    {
        if (_httpClient is not null) return _httpClient;
        _app.UseMiddleware<GlobalExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.Use(async (context, next) =>
        {
            if (!context.Request.Headers.ContainsKey("X-Test-Discard-Response")) { await next(context); return; }
            // Discard the HTTP receipt after the real endpoint commits to simulate a lost response.
            var stream = context.Response.Body;
            await using var discarded = new MemoryStream();
            context.Response.Body = discarded;
            try
            {
                await next(context);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
            finally { context.Response.Body = stream; }
        });
        _app.MapReviewerAssignmentEndpoints();
        await _app.StartAsync();
        return _httpClient = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
    }

    private async Task<object> BodyAsync(Guid? reviewer = null, Guid? evaluation = null, int version = 2)
    {
        await using var db = Db<DefenseCouncilDbContext>();
        return new
        {
            groupId = _group,
            reviewerId = reviewer ?? _reviewer,
            expectedAdvisorEvaluationId = evaluation ?? (await db.AdvisorEvaluations.SingleAsync()).Id,
            expectedReportVersionNo = version
        };
    }

    private async Task<HttpResponseMessage> PostAsync(object? body = null, Guid? key = null,
        string role = "Admin", string? permission = null, Guid? actor = null)
    {
        var client = await HttpAsync();
        using var request = Request(body ?? await BodyAsync(), key ?? Guid.NewGuid(), role, permission, actor);
        return await client.SendAsync(request);
    }

    private HttpRequestMessage Request(object body, Guid key, string role, string? permission = null, Guid? actor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, CreationRoute) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{actor ?? _actor:N}|{role}|{permission}");
        request.Headers.Add("Idempotency-Key", key.ToString());
        return request;
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Faculty")]
    [InlineData("AcademicAffairs")]
    public async Task Creation_http_commits_the_approved_snapshot_actor_receipt_and_audit_outside_student_deadlines(string role)
    {
        var before = DateTime.UtcNow;
        using var response = await PostAsync(role: role,
            permission: role == "Admin" ? null : PermissionCodes.ThesisReviewerAssignmentsManage);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, payload);
        Assert.Null(response.Headers.Location);
        using var json = JsonDocument.Parse(payload);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(_group, data.GetProperty("groupId").GetGuid());
        Assert.Equal(_topic, data.GetProperty("topicId").GetGuid());
        Assert.Equal(_batch, data.GetProperty("batchId").GetGuid());
        Assert.Equal("Reviewer transaction", data.GetProperty("topicTitleVi").GetString());
        Assert.Equal("Lecturer 1", data.GetProperty("reviewerFullName").GetString());
        Assert.Equal(_advisor, data.GetProperty("advisorIdAtAssignment").GetGuid());
        Assert.Equal(_file, data.GetProperty("submissionFileId").GetGuid());
        Assert.Equal(Checksum, data.GetProperty("submissionChecksumSha256").GetString());
        Assert.Equal(2, data.GetProperty("reportVersionNo").GetInt32());
        Assert.Equal(1, data.GetProperty("assignmentOrder").GetInt32());
        Assert.Equal(_actor, data.GetProperty("assignedBy").GetGuid());
        Assert.InRange(data.GetProperty("assignedAtUtc").GetDateTime(), before, DateTime.UtcNow);
        Assert.True(data.GetProperty("isActive").GetBoolean());
        Assert.False(data.GetProperty("isLegacy").GetBoolean());
        Assert.True(data.GetProperty("assignmentVersion").GetUInt32() > 0);
        foreach (var hidden in new[] { "score", "comment", "objectKey", "downloadUrl", "canCancel", "hasEvaluation" })
            Assert.False(data.TryGetProperty(hidden, out _));
        await AssertCountsAsync(1, 1, 1);
        await using var db = Db<DefenseCouncilDbContext>();
        Assert.Equal(data.GetProperty("assignmentVersion").GetUInt32(), (await db.ReviewerAssignments.SingleAsync()).Version);
        var receipt = await db.ReviewerAssignmentIdempotencyRecords.SingleAsync();
        Assert.Equal(201, receipt.ResponseStatusCode);
        Assert.Equal(_actor, receipt.ActorId);
        var auditEvent = JsonSerializer.Deserialize<AuditIntegrationEvent>((await db.AuditOutboxMessages.SingleAsync()).PayloadJson)!;
        Assert.Equal(_actor, auditEvent.ActorUserId);
        Assert.Equal(IntegrationAuditActions.ReviewerAssignmentCreated, auditEvent.Action);
        await using var thesis = Db<ThesisManagementDbContext>();
        Assert.Equal(StudentGroupStatus.ReadyForReviewerAssignment, (await thesis.StudentGroups.SingleAsync()).Status);
        Assert.Equal(FinalReportStatus.UnderEvaluation, (await thesis.FinalReports.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("Student", true)]
    [InlineData("Lecturer", true)]
    [InlineData("Advisor", true)]
    [InlineData("Reviewer", true)]
    [InlineData("Faculty", false)]
    [InlineData("AcademicAffairs", false)]
    public async Task Creation_http_denies_roles_or_read_only_permission_before_parsing_or_resource_lookup(string role, bool manage)
    {
        var client = await HttpAsync();
        using var request = Request(new { }, Guid.NewGuid(), role,
            manage ? PermissionCodes.ThesisReviewerAssignmentsManage : PermissionCodes.ThesisReviewerAssignmentsRead);
        request.Content = new StringContent("invalid json", Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertCountsAsync(0, 0, 0);
    }

    [Fact]
    public async Task Creation_http_requires_authentication_and_strict_header_json_and_body_in_development_too()
    {
        var client = await HttpAsync();
        using (var anonymous = await client.PostAsJsonAsync(CreationRoute, new { }))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var body = await BodyAsync();
        foreach (var header in new[] { "missing", "", "invalid", Guid.Empty.ToString(), "multiple" })
        {
            using var request = Request(body, Guid.NewGuid(), "Admin");
            request.Headers.Remove("Idempotency-Key");
            if (header != "missing") request.Headers.TryAddWithoutValidation("Idempotency-Key",
                header == "multiple" ? new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() } : new[] { header });
            using var response = await client.SendAsync(request);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest");
        }
        foreach (var json in new[] { "null", "{}", "{", "[]", "{\"expectedReportVersionNo\":1.5}" })
        {
            using var request = Request(body, Guid.NewGuid(), "Admin");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest");
        }
        await using (var db = Db<DefenseCouncilDbContext>())
        {
            var evaluation = (await db.AdvisorEvaluations.SingleAsync()).Id;
            foreach (var field in new[] { "group", "reviewer", "evaluation", "version", "string-version" })
            {
                using var invalid = await PostAsync(new
                {
                    groupId = field == "group" ? Guid.Empty : _group,
                    reviewerId = field == "reviewer" ? Guid.Empty : _reviewer,
                    expectedAdvisorEvaluationId = field == "evaluation" ? Guid.Empty : evaluation,
                    expectedReportVersionNo = field == "string-version" ? (object)"2" : field == "version" ? 0 : 2
                });
                await AssertErrorAsync(invalid, HttpStatusCode.BadRequest, "InvalidRequest");
            }
        }
        using (var request = Request(body, Guid.NewGuid(), "Admin"))
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "text/plain");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }
        await AssertCountsAsync(0, 0, 0);
    }

    [Theory]
    [InlineData(false, "Admin", false, "ReviewerAssignment.AuthenticationRequired")]
    [InlineData(true, "Lecturer", true, "ReviewerAssignment.AccessDenied")]
    [InlineData(true, "Faculty", false, "ReviewerAssignment.AccessDenied")]
    public async Task Creation_handler_independently_authorizes_before_input_or_resource_validation(
        bool authenticated, string role, bool manage, string code)
    {
        var accessor = _app.Services.GetRequiredService<IHttpContextAccessor>();
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, _actor.ToString()), new(ClaimTypes.Role, role)];
        if (manage) claims.Add(new("permission", PermissionCodes.ThesisReviewerAssignmentsManage));
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticated ? "DirectHandlerTest" : null))
        };
        try
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<CreateReviewerAssignmentHandler>()
                .HandleAsync(new(Guid.Empty, Guid.Empty, Guid.Empty, 0, Guid.Empty));
            Assert.True(result.IsFailure);
            Assert.Equal(code, result.Error.Code);
            await AssertCountsAsync(0, 0, 0);
        }
        finally { accessor.HttpContext = null; }
    }

    [Theory]
    [InlineData("group", "GroupNotFound", 404)]
    [InlineData("context", "ContextInvalid", 409)]
    [InlineData("pending", "NotEligible", 409)]
    [InlineData("no-approval", "NotEligible", 409)]
    [InlineData("legacy", "NotEligible", 409)]
    [InlineData("group-state", "NotEligible", 409)]
    [InlineData("file", "NotEligible", 409)]
    [InlineData("batch", "BatchNotOpen", 409)]
    [InlineData("evaluation", "BindingConflict", 409)]
    [InlineData("version", "BindingConflict", 409)]
    [InlineData("missing-reviewer", "ReviewerNotFound", 404)]
    [InlineData("inactive", "ReviewerNotEligible", 409)]
    [InlineData("nonlecturer", "ReviewerNotEligible", 409)]
    [InlineData("advisor", "AdvisorConflict", 409)]
    public async Task Creation_http_enforces_business_guards_for_admin_and_keeps_failures_mutation_free(string scenario, string code, int status)
    {
        await using var db = Db<DefenseCouncilDbContext>();
        var evaluation = (await db.AdvisorEvaluations.SingleAsync()).Id;
        var reviewer = scenario switch { "missing-reviewer" => Guid.NewGuid(), "nonlecturer" => _student, "advisor" => _advisor, _ => _reviewer };
        await using (var writer = Db<UGTM.Database.Migrations.UGTMDbContext>())
        {
            if (scenario == "context") await writer.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM thesis.student_group_member WHERE group_id = {_group}");
            if (scenario == "pending") await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE councils.advisor_evaluation SET allows_reviewer_assignment = NULL, decision_by = NULL, decision_at = NULL WHERE group_id = {_group}");
            if (scenario == "no-approval") await writer.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM councils.advisor_evaluation WHERE group_id = {_group}");
            if (scenario == "legacy") await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE councils.advisor_evaluation SET is_legacy = true WHERE group_id = {_group}");
            if (scenario == "group-state") await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.student_group SET status = 'Stopped' WHERE id = {_group}");
            if (scenario == "file") await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.submission_file SET checksum_sha256 = {new string('f', 64)} WHERE id = {_file}");
            if (scenario == "batch") await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic.kltn_batch SET status = 'Closed' WHERE id = {_batch}");
            if (scenario == "inactive") await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE identity.\"user\" SET status = 'Suspended' WHERE id = {_reviewer}");
        }
        using var response = await PostAsync(new
        {
            groupId = scenario == "group" ? Guid.NewGuid() : _group,
            reviewerId = reviewer,
            expectedAdvisorEvaluationId = scenario == "evaluation" ? Guid.NewGuid() : evaluation,
            expectedReportVersionNo = scenario == "version" ? 1 : 2
        });
        await AssertErrorAsync(response, (HttpStatusCode)status, code);
        await AssertCountsAsync(0, 0, 0);
    }

    [Fact]
    public async Task Creation_http_prioritizes_binding_before_missing_reviewer_and_ignores_client_controlled_fields()
    {
        using (var stale = await PostAsync(await BodyAsync(Guid.NewGuid(), Guid.NewGuid())))
            await AssertErrorAsync(stale, HttpStatusCode.Conflict, "BindingConflict");
        await using var db = Db<DefenseCouncilDbContext>();
        using var response = await PostAsync(new
        {
            groupId = _group,
            reviewerId = _reviewer,
            expectedAdvisorEvaluationId = (await db.AdvisorEvaluations.SingleAsync()).Id,
            expectedReportVersionNo = 2,
            advisorId = _reviewer,
            assignedBy = _student,
            assignedAt = Now,
            assignmentOrder = 2,
            isActive = false,
            submissionFileId = _oldFile,
            score = 10,
            comment = "client"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var assignment = await db.ReviewerAssignments.SingleAsync();
        Assert.Equal(_actor, assignment.AssignedBy);
        Assert.Equal(_advisor, assignment.AdvisorIdAtAssignment);
        Assert.Equal(_file, assignment.SubmissionFileId);
        Assert.Equal(1, assignment.AssignmentOrder);
        Assert.True(assignment.IsActive);
    }

    [Fact]
    public async Task Creation_http_replays_original_receipt_after_cancel_and_business_changes_without_reactivating()
    {
        var body = await BodyAsync();
        var key = Guid.NewGuid();
        using var created = await PostAsync(body, key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadAsStringAsync();
        await using (var db = Db<DefenseCouncilDbContext>())
        {
            var assignment = await db.ReviewerAssignments.SingleAsync();
            Assert.True(assignment.TryCancel(_actor, DateTime.UtcNow, "Sửa phân công", false));
            await db.SaveChangesAsync();
        }
        await using (var writer = Db<UGTM.Database.Migrations.UGTMDbContext>())
            await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic.kltn_batch SET status = 'Closed' WHERE id = {_batch}");
        using var retry = await PostAsync(body, key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(original, await retry.Content.ReadAsStringAsync());
        using var reused = await PostAsync(await BodyAsync(_otherReviewer), key);
        await AssertErrorAsync(reused, HttpStatusCode.Conflict, "IdempotencyKeyReused");
        using var forbidden = await PostAsync(body, key, "Faculty", PermissionCodes.ThesisReviewerAssignmentsRead);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await AssertCountsAsync(1, 1, 1);
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.False((await verify.ReviewerAssignments.SingleAsync()).IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Creation_http_concurrent_requests_create_one_assignment_receipt_and_audit(bool sameKey)
    {
        var body = await BodyAsync();
        await HttpAsync();
        var key = Guid.NewGuid();
        var responses = await Task.WhenAll(PostAsync(body, key), PostAsync(body, sameKey ? key : Guid.NewGuid()));
        try
        {
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
            if (sameKey)
            {
                Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
                Assert.Equal(await responses[0].Content.ReadAsStringAsync(), await responses[1].Content.ReadAsStringAsync());
            }
            else await AssertErrorAsync(responses.Single(response => response.StatusCode != HttpStatusCode.Created),
                HttpStatusCode.Conflict, "AlreadyAssigned");
            await AssertCountsAsync(1, 1, 1);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Creation_http_normalizes_json_order_and_guid_format_and_scopes_keys_by_actor()
    {
        var client = await HttpAsync();
        var body = await BodyAsync();
        var key = Guid.NewGuid();
        using var created = await PostAsync(body, key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await using var db = Db<DefenseCouncilDbContext>();
        var evaluation = (await db.AdvisorEvaluations.SingleAsync()).Id;
        using var request = Request(body, key, "Admin");
        request.Content = new StringContent($"{{ \"expectedReportVersionNo\": 2, \"expectedAdvisorEvaluationId\": \"{evaluation.ToString().ToUpperInvariant()}\", \"reviewerId\": \"{_reviewer.ToString().ToUpperInvariant()}\", \"groupId\": \"{_group}\" }}", Encoding.UTF8, "application/json");
        using var replay = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await created.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var otherActor = await PostAsync(body, key, actor: _advisor);
        await AssertErrorAsync(otherActor, HttpStatusCode.Conflict, "AlreadyAssigned");
        await AssertCountsAsync(1, 1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Creation_http_rolls_back_assignment_receipt_and_audit_on_either_save_failure_then_allows_retry(bool secondSave)
    {
        var body = await BodyAsync();
        var key = Guid.NewGuid();
        await using var db = Db<DefenseCouncilDbContext>();
        var failureSql = secondSave ? """
            ALTER TABLE councils.reviewer_assignment_idempotency ADD CONSTRAINT force_reviewer_create_failure
            CHECK (response_payload_json = '{{}}'::jsonb)
            """ : """
            ALTER TABLE councils.audit_outbox_message ADD CONSTRAINT force_reviewer_create_failure
            CHECK (action <> 'DefenseCouncil.ReviewerAssignment.Created')
            """;
        await db.Database.ExecuteSqlRawAsync(failureSql);
        using var failed = await PostAsync(body, key);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.DoesNotContain("force_reviewer_create_failure", await failed.Content.ReadAsStringAsync());
        await AssertCountsAsync(0, 0, 0);
        await db.Database.ExecuteSqlRawAsync(secondSave
            ? "ALTER TABLE councils.reviewer_assignment_idempotency DROP CONSTRAINT force_reviewer_create_failure"
            : "ALTER TABLE councils.audit_outbox_message DROP CONSTRAINT force_reviewer_create_failure");
        using var retry = await PostAsync(body, key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        await AssertCountsAsync(1, 1, 1);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status}, got {response.StatusCode}: {content}");
        using var json = JsonDocument.Parse(content);
        Assert.Equal($"ReviewerAssignment.{code}", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Creation_http_recovers_a_committed_receipt_when_the_first_response_is_lost()
    {
        var client = await HttpAsync();
        var body = await BodyAsync();
        var key = Guid.NewGuid();
        using var request = Request(body, key, "Admin");
        request.Headers.Add("X-Test-Discard-Response", "true");
        using var lost = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, lost.StatusCode);
        await AssertCountsAsync(1, 1, 1);
        using var retry = await PostAsync(body, key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        using var json = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        await using var db = Db<DefenseCouncilDbContext>();
        var assignment = await db.ReviewerAssignments.SingleAsync();
        Assert.Equal(assignment.Id, json.RootElement.GetProperty("data").GetProperty("id").GetGuid());
        Assert.Equal(assignment.Version, json.RootElement.GetProperty("data").GetProperty("assignmentVersion").GetUInt32());
        await AssertCountsAsync(1, 1, 1);
    }

    private sealed class CreationAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "ReviewerCreationTest";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var value = Request.Headers.Authorization.ToString();
            if (!value.StartsWith("Bearer ", StringComparison.Ordinal)) return Task.FromResult(AuthenticateResult.NoResult());
            var parts = value[7..].Split('|');
            if (parts.Length != 3 || !Guid.TryParse(parts[0], out var id))
                return Task.FromResult(AuthenticateResult.Fail("Invalid test token"));
            List<Claim> claims = [new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Role, parts[1])];
            if (!string.IsNullOrEmpty(parts[2])) claims.Add(new("permission", parts[2]));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme)), Scheme)));
        }
    }
}
