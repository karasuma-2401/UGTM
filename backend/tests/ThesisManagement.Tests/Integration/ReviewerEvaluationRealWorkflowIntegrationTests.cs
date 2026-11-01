using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Evaluation_cp2_real_report_advisor_assignment_draft_submit_flow_preserves_file_access_and_private_grade_boundary(bool admin)
    {
        await using (var identity = Db<IdentityDbContext>())
        {
            var lecturer = await identity.Roles.SingleAsync(value => value.RoleName == "Lecturer");
            identity.UserRoles.Add(new UserRole { UserId = _replacementAdvisorId, RoleId = lecturer.Id, AssignedAt = SeedTime });
            await identity.SaveChangesAsync();
        }
        using var v1 = await SubmitAsync("pdf", _studentId); using var v2 = await SubmitAsync("docx", _partnerId);
        Assert.Equal(HttpStatusCode.OK, v1.StatusCode); Assert.Equal(HttpStatusCode.OK, v2.StatusCode);
        Guid reportId, fileId;
        await using (var thesis = Db<ThesisManagementDbContext>())
        {
            reportId = (await thesis.FinalReports.SingleAsync()).Id;
            fileId = (await thesis.SubmissionFiles.SingleAsync(value => value.VersionNo == 2)).Id;
        }
        using var locked = await BeginAsync(reportId, 2, _advisorId); Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        using var advisor = await SaveAdvisorEvaluationAsync(reportId, 2, null, 8.5m, "Nhận xét riêng GVHD", Guid.NewGuid(), _advisorId, "Lecturer");
        using var approved = await DecideAdvisorEvaluationAsync(reportId, 2, EvaluationVersion(await advisor.Content.ReadAsStringAsync()), true, null,
            Guid.NewGuid(), _advisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        _clock.UtcNow = _deadlineAtUtc.AddDays(1); // Grading does not reuse the student's expired upload deadline.
        using var assigned = await WriteReviewerAsync(HttpMethod.Post, "/api/thesis/reviewer-assignments", new
        {
            groupId = _groupId,
            reviewerId = _replacementAdvisorId,
            expectedAdvisorEvaluationId = (await ReadDataAsync(approved)).GetProperty("id").GetGuid(),
            expectedReportVersionNo = 2
        });
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        var parent = await ReadDataAsync(assigned); var assignmentId = parent.GetProperty("id").GetGuid();
        var parentVersion = parent.GetProperty("assignmentVersion").GetUInt32();
        using var draft = await SaveAsync(null, false, _replacementAdvisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var initial = await ReadDataAsync(draft); Assert.Equal(JsonValueKind.Null, initial.GetProperty("score").ValueKind);
        using (var cancel = await CancelAsync(parent)) Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        using (var studentRead = await SendAsync(HttpMethod.Get, $"/api/thesis/reviewer-assignments/{assignmentId}/evaluation", _studentId, "Student"))
            Assert.Equal(HttpStatusCode.Forbidden, studentRead.StatusCode);
        using (var advisorRead = await SendAsync(HttpMethod.Get, $"/api/thesis/reviewer-assignments/{assignmentId}/evaluation", _advisorId, "Lecturer"))
            Assert.Equal(HttpStatusCode.Forbidden, advisorRead.StatusCode);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(10);
        using var final = await SaveAsync(initial.GetProperty("evaluationVersion").GetUInt32(), true,
            admin ? _adminId : _replacementAdvisorId, admin ? "Admin" : "Lecturer");
        Assert.Equal(HttpStatusCode.OK, final.StatusCode);
        var data = await ReadDataAsync(final);
        Assert.Equal(admin ? _adminId : _replacementAdvisorId, data.GetProperty("submittedBy").GetGuid());
        Assert.Equal(_clock.UtcNow, data.GetProperty("submittedAtUtc").GetDateTime());
        Assert.Equal(fileId, data.GetProperty("submissionFileId").GetGuid());
        Assert.Equal(8.35m, data.GetProperty("score").GetDecimal());
        Assert.Equal("Submitted", data.GetProperty("status").GetString());
        using var read = await SendAsync(HttpMethod.Get, $"/api/thesis/reviewer-assignments/{assignmentId}/evaluation", _replacementAdvisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.DoesNotContain("Nhận xét riêng GVHD", await read.Content.ReadAsStringAsync());
        await AssertDownloadAsync(fileId, Document("docx"), _replacementAdvisorId, "Lecturer");
        using var advisorDenied = await GetAdvisorEvaluationAsync(_replacementAdvisorId, "Lecturer"); Assert.Equal(HttpStatusCode.Forbidden, advisorDenied.StatusCode);
        await using var defense = Db<DefenseCouncilDbContext>();
        Assert.Single(await defense.ReviewerEvaluations.ToListAsync()); Assert.Equal(2, await defense.ReviewerEvaluationIdempotencyRecords.CountAsync());
        var audit = JsonSerializer.Deserialize<AuditIntegrationEvent>((await defense.AuditOutboxMessages.SingleAsync(value => value.Action == IntegrationAuditActions.ReviewerEvaluationSubmitted)).PayloadJson)!;
        Assert.Equal(admin ? _adminId : _replacementAdvisorId, audit.ActorUserId); Assert.DoesNotContain("Góp ý", audit.MetadataJson);
        if (!admin) await AssertReviewerEvaluationOpenApiAsync();

        async Task<HttpResponseMessage> SaveAsync(uint? evaluationVersion, bool submit, Guid actor, string role)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/thesis/reviewer-evaluations")
            {
                Content = JsonContent.Create(new
                {
                    assignmentId,
                    expectedAssignmentVersion = parentVersion,
                    expectedEvaluationVersion = evaluationVersion,
                    action = submit ? "Submit" : "SaveDraft",
                    score = submit ? (decimal?)8.345m : null,
                    comment = submit ? "Góp ý đã chốt" : "Góp ý nháp",
                    defenseApproval = submit ? (admin ? "Rejected" : "Approved") : null
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{actor:N}.{role}");
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); return await _client.SendAsync(request);
        }
    }

    private async Task AssertReviewerEvaluationOpenApiAsync()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync(); Assert.True(response.StatusCode == HttpStatusCode.OK, content);
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "reviewer-evaluation-openapi.json"), content);
        using var json = JsonDocument.Parse(content); var paths = json.RootElement.GetProperty("paths");
        var post = paths.GetProperty("/api/thesis/reviewer-evaluations").GetProperty("post");
        var key = Assert.Single(post.GetProperty("parameters").EnumerateArray(), value => value.GetProperty("name").GetString() == "Idempotency-Key");
        Assert.True(key.GetProperty("required").GetBoolean()); Assert.Equal("header", key.GetProperty("in").GetString());
        Assert.Equal("uuid", key.GetProperty("schema").GetProperty("format").GetString());
        var body = post.GetProperty("requestBody"); Assert.True(body.GetProperty("required").GetBoolean());
        var schema = body.GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "action", "assignmentId", "expectedAssignmentVersion" }, schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).Order().ToArray());
        var properties = schema.GetProperty("properties"); Assert.Equal(7, properties.EnumerateObject().Count());
        Assert.Equal(new[] { "SaveDraft", "Submit" }, properties.GetProperty("action").GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Equal(0, properties.GetProperty("score").GetProperty("minimum").GetDecimal());
        Assert.Equal(10, properties.GetProperty("score").GetProperty("maximum").GetDecimal());
        foreach (var status in new[] { "200", "201", "400", "401", "403", "404", "409", "413", "415" }) Assert.True(post.GetProperty("responses").TryGetProperty(status, out _));
        var get = paths.GetProperty("/api/thesis/reviewer-assignments/{assignmentId}/evaluation").GetProperty("get");
        Assert.DoesNotContain(get.GetProperty("parameters").EnumerateArray(), value => value.GetProperty("in").GetString() is "query" or "header");
        foreach (var status in new[] { "200", "400", "401", "403", "404", "409" }) Assert.True(get.GetProperty("responses").TryGetProperty(status, out _));
        var dto = json.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ReviewerEvaluationDto").GetProperty("properties");
        Assert.Equal(27, dto.EnumerateObject().Count());
        Assert.Equal(new[] { "Draft", "Submitted" }, dto.GetProperty("status").GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToArray());
        foreach (var excluded in new[] { "advisorScore", "advisorComment", "objectKey", "downloadUrl", "canProceed", "revisionFeedback", "exclusionReason" }) Assert.False(dto.TryGetProperty(excluded, out _));
    }
}
