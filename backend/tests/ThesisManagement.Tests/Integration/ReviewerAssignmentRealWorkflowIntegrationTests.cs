using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Real_reviewer_workflow_preserves_snapshot_history_audit_and_grade_boundary(bool sameReviewer)
    {
        // Actual persisted identities/grants, production owner readers/coordinator, PostgreSQL FKs and S3.
        await using (var identity = Db<IdentityDbContext>())
        {
            var role = await identity.Roles.SingleAsync(value => value.RoleName == "Lecturer");
            identity.UserRoles.AddRange(new[] { _replacementAdvisorId, _adminId }.Select(id => new UserRole
            { UserId = id, RoleId = role.Id, AssignedAt = SeedTime }));
            await identity.SaveChangesAsync();
        }
        using var v1 = await SubmitAsync("pdf", _studentId);
        using var v2 = await SubmitAsync("docx", _partnerId);
        Assert.Equal(HttpStatusCode.OK, v1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, v2.StatusCode);
        Guid reportId, oldFileId, approvedFileId;
        await using (var thesis = Db<ThesisManagementDbContext>())
        {
            reportId = (await thesis.FinalReports.SingleAsync()).Id;
            oldFileId = (await thesis.SubmissionFiles.SingleAsync(value => value.VersionNo == 1)).Id;
            approvedFileId = (await thesis.SubmissionFiles.SingleAsync(value => value.VersionNo == 2)).Id;
        }
        using var locked = await BeginAsync(reportId, 2, _advisorId);
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        var decisionActor = sameReviewer ? _advisorId : _adminId;
        var decisionRole = sameReviewer ? "Lecturer" : "Admin";
        using var saved = await SaveAdvisorEvaluationAsync(reportId, 2, null, 8.5m, "Nhận xét riêng của GVHD",
            Guid.NewGuid(), decisionActor, decisionRole);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var approved = await DecideAdvisorEvaluationAsync(reportId, 2,
            EvaluationVersion(await saved.Content.ReadAsStringAsync()), true, null, Guid.NewGuid(), decisionActor, decisionRole);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var evaluationId = (await ReadDataAsync(approved)).GetProperty("id").GetGuid();
        // Assignment is an administrative operation independent of the student's expired submission window.
        _clock.UtcNow = _deadlineAtUtc.AddDays(1);
        using var tooLateToSubmit = await SubmitAsync("pdf", _studentId);
        Assert.Equal(HttpStatusCode.Conflict, tooLateToSubmit.StatusCode);

        var first = await AssignAsync(_replacementAdvisorId);
        var firstId = first.GetProperty("id").GetGuid();
        Assert.Equal(_adminId, first.GetProperty("assignedBy").GetGuid());
        Assert.Equal(approvedFileId, first.GetProperty("submissionFileId").GetGuid());
        Assert.Equal(2, first.GetProperty("reportVersionNo").GetInt32());
        await AssertDownloadAsync(approvedFileId, Document("docx"), _replacementAdvisorId, "Lecturer");
        using var oldVersion = await SendAsync(HttpMethod.Get, DownloadRoute(oldFileId), _replacementAdvisorId, "Lecturer");
        using var historyDenied = await SendAsync(HttpMethod.Get, HistoryRoute, _replacementAdvisorId, "Lecturer");
        using var advisorDenied = await GetAdvisorEvaluationAsync(_replacementAdvisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.Forbidden, oldVersion.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, historyDenied.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, advisorDenied.StatusCode);
        using var tasks = await SendAsync(HttpMethod.Get, "/api/thesis/me/reviewer-assignments", _replacementAdvisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.OK, tasks.StatusCode);
        var tasksJson = await tasks.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Nhận xét riêng", tasksJson);
        Assert.DoesNotContain("advisorScore", tasksJson);
        Assert.DoesNotContain("objectKey", tasksJson);

        using var link = await SendAsync(HttpMethod.Get, DownloadRoute(approvedFileId), _replacementAdvisorId, "Lecturer");
        var issuedUrl = (await ReadDataAsync(link)).GetProperty("downloadUrl").GetString()!;
        using var cancelled = await CancelAsync(first);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal(_adminId, (await ReadDataAsync(cancelled)).GetProperty("cancelledBy").GetGuid());
        using var newLinkDenied = await SendAsync(HttpMethod.Get, DownloadRoute(approvedFileId), _replacementAdvisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.Forbidden, newLinkDenied.StatusCode);
        using var anonymous = new HttpClient();
        Assert.Equal(Document("docx"), await anonymous.GetByteArrayAsync(issuedUrl)); // Issued URLs live to their TTL.

        var nextReviewer = sameReviewer ? _replacementAdvisorId : _adminId;
        var second = await AssignAsync(nextReviewer);
        var secondId = second.GetProperty("id").GetGuid();
        Assert.NotEqual(firstId, secondId);
        await AssertDownloadAsync(approvedFileId, Document("docx"), nextReviewer, "Lecturer");
        if (!sameReviewer)
        {
            using var formerReviewer = await SendAsync(HttpMethod.Get, DownloadRoute(approvedFileId), _replacementAdvisorId, "Lecturer");
            Assert.Equal(HttpStatusCode.Forbidden, formerReviewer.StatusCode);
        }
        // Simulate the future SCRUM-105 writer through the shared locking contract, not a new grading endpoint.
        using (var scope = _app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            Assert.True(await services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteAsync(async token =>
            {
                var access = await services.GetRequiredService<IReviewerAssignmentWorkflowContract>()
                    .LockForEvaluationAsync(secondId, nextReviewer, token);
                Assert.NotNull(access);
                var defense = services.GetRequiredService<DefenseCouncilDbContext>();
                var sheet = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), access.AssignmentId, 0m, "Phiếu đã lưu", DefenseApprovalStatus.Approved, nextReviewer, _clock.UtcNow);
                defense.ReviewerEvaluations.Add(sheet);
                defense.Entry(sheet).Property(value => value.IsIgnoredInCalculation).CurrentValue = true;
                defense.Entry(sheet).Property(value => value.ExclusionReason).CurrentValue = "Không tính điểm";
                return ReviewerAssignmentTransactionResult<bool>.Commit(true);
            }));
        }
        await using (var academic = Db<AcademicPeriodsDbContext>())
        {
            (await academic.KltnBatches.SingleAsync()).Close(_adminId, _clock.UtcNow);
            await academic.SaveChangesAsync();
        }
        using var gradeBlocksCancellation = await CancelAsync(second);
        Assert.Equal(HttpStatusCode.Conflict, gradeBlocksCancellation.StatusCode);
        Assert.Contains("ReviewerAssignment.EvaluationExists", await gradeBlocksCancellation.Content.ReadAsStringAsync());
        await AssertDownloadAsync(approvedFileId, Document("docx"), nextReviewer, "Lecturer");
        // Independent student/advisor rights survive cancellation; no report/group reopens.
        await AssertDownloadAsync(oldFileId, Document("pdf"), _studentId, "Student");
        await AssertDownloadAsync(approvedFileId, Document("docx"), _advisorId, "Lecturer");
        await using var defenseVerify = Db<DefenseCouncilDbContext>();
        var rows = await defenseVerify.ReviewerAssignments.OrderBy(value => value.IsActive).ToArrayAsync();
        Assert.Equal(2, rows.Length);
        Assert.False(rows[0].IsActive);
        Assert.Equal(firstId, rows[0].Id);
        Assert.True(rows[1].IsActive);
        Assert.Equal(secondId, rows[1].Id);
        Assert.Equal(second.GetProperty("assignmentVersion").GetUInt32(), rows[1].Version);
        Assert.Equal(1, await defenseVerify.ReviewerEvaluations.CountAsync());
        var grade = await defenseVerify.ReviewerEvaluations.SingleAsync();
        Assert.Equal(secondId, grade.AssignmentId);
        Assert.Equal(0m, grade.Score);
        Assert.True(grade.IsIgnoredInCalculation);
        Assert.False(grade.IsVisibleToStudent);
        var advisorEvaluation = await defenseVerify.AdvisorEvaluations.SingleAsync();
        Assert.True(advisorEvaluation.IsLocked);
        Assert.Equal(evaluationId, advisorEvaluation.Id);
        Assert.Equal(decisionActor, advisorEvaluation.DecisionBy);
        Assert.Equal(8.5m, advisorEvaluation.Score);
        Assert.Equal(3, await defenseVerify.ReviewerAssignmentIdempotencyRecords.CountAsync());
        var audits = await defenseVerify.AuditOutboxMessages.Where(value =>
            value.Action == IntegrationAuditActions.ReviewerAssignmentCreated || value.Action == IntegrationAuditActions.ReviewerAssignmentCancelled).ToArrayAsync();
        Assert.Equal(3, audits.Length);
        Assert.Equal(2, audits.Count(value => value.Action == IntegrationAuditActions.ReviewerAssignmentCreated));
        Assert.Single(audits, value => value.Action == IntegrationAuditActions.ReviewerAssignmentCancelled);
        Assert.All(audits, audit =>
        {
            Assert.Equal(_adminId, JsonSerializer.Deserialize<AuditIntegrationEvent>(audit.PayloadJson)!.ActorUserId);
            Assert.DoesNotContain("Điều chỉnh phân công", audit.PayloadJson);
        });
        await using var thesisVerify = Db<ThesisManagementDbContext>();
        Assert.Equal(StudentGroupStatus.ReadyForReviewerAssignment, (await thesisVerify.StudentGroups.SingleAsync()).Status);
        Assert.Equal(FinalReportStatus.UnderEvaluation, (await thesisVerify.FinalReports.SingleAsync()).Status);
        Assert.Equal(2, await thesisVerify.SubmissionFiles.CountAsync());
        Assert.Equal(2, (await StoredKeysAsync()).Count);
        if (sameReviewer) await AssertReviewerOpenApiAsync();

        async Task<JsonElement> AssignAsync(Guid reviewerId)
        {
            using var response = await WriteReviewerAsync(HttpMethod.Post, "/api/thesis/reviewer-assignments",
                new { groupId = _groupId, reviewerId, expectedAdvisorEvaluationId = evaluationId, expectedReportVersionNo = 2 });
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return await ReadDataAsync(response);
        }
    }

    private Task<HttpResponseMessage> CancelAsync(JsonElement assignment)
        => WriteReviewerAsync(HttpMethod.Delete, $"/api/thesis/reviewer-assignments/{assignment.GetProperty("id").GetGuid()}",
            new { expectedAssignmentVersion = assignment.GetProperty("assignmentVersion").GetUInt32(), reason = "Điều chỉnh phân công" });

    private async Task<HttpResponseMessage> WriteReviewerAsync(HttpMethod method, string path, object body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{_adminId:N}.Admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }

    private async Task AssertReviewerOpenApiAsync()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var content = await response.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "reviewer-openapi.json"), content);
        using var json = JsonDocument.Parse(content);
        var paths = json.RootElement.GetProperty("paths");
        foreach (var (path, method) in new[] { ("/api/thesis/reviewer-assignments", "post"), ("/api/thesis/reviewer-assignments/{id}", "delete") })
        {
            var operation = paths.GetProperty(path).GetProperty(method);
            var key = Assert.Single(operation.GetProperty("parameters").EnumerateArray(), value => value.GetProperty("name").GetString() == "Idempotency-Key");
            Assert.True(key.GetProperty("required").GetBoolean());
            Assert.Equal("uuid", key.GetProperty("schema").GetProperty("format").GetString());
            Assert.True(operation.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
        }
        foreach (var (path, required, count) in new[]
        {
            ("/api/thesis/reviewer-assignments", "", 8),
            ("/api/thesis/reviewer-assignment-eligibility", "batchId", 5),
            ("/api/thesis/reviewer-candidates", "groupId", 4),
            ("/api/thesis/me/reviewer-assignments", "", 4)
        })
        {
            var parameters = paths.GetProperty(path).GetProperty("get").GetProperty("parameters").EnumerateArray().ToArray();
            Assert.Equal(count, parameters.Length);
            if (required.Length > 0) Assert.True(parameters.Single(value => value.GetProperty("name").GetString() == required).GetProperty("required").GetBoolean());
        }
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");
        var item = schemas.GetProperty("ReviewerAssignmentListItemDto").GetProperty("properties");
        foreach (var field in new[] { "id", "assignmentVersion", "hasEvaluation", "canCancel", "cancelledBy" })
            Assert.True(item.TryGetProperty(field, out _), field);
    }
}
