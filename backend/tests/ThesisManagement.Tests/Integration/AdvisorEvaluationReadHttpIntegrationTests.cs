using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportSubmissionHttpIntegrationTests
{
    private static string AdvisorEvaluationReadRoute(Guid groupId)
        => $"/api/thesis/groups/{groupId}/advisor-evaluation";

    [Fact]
    public async Task Advisor_evaluation_read_returns_current_data_to_current_advisor_and_admin_without_mutation()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        await SeedIdentityUserAsync(advisorId, "advisor@test.example", "Advisor");
        using var start = await StartEvaluationAsync(report.Id, 1, advisorId);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var saved = await SaveAdvisorEvaluationAsync(
            report.Id, 1, null, 8.35m, "Nhận xét chỉ dành cho GVHD.", Guid.NewGuid(), advisorId);
        var savedContent = await saved.Content.ReadAsStringAsync();
        Assert.True(saved.StatusCode == HttpStatusCode.OK, savedContent);
        var savedVersion = EvaluationVersion(savedContent);

        int defenseAuditCount;
        int defenseIdempotencyCount;
        int thesisAuditCount;
        await using (var defense = CreateDefenseDbContext())
        {
            defenseAuditCount = await defense.AuditOutboxMessages.CountAsync();
            defenseIdempotencyCount = await defense.AdvisorEvaluationIdempotencyRecords.CountAsync();
        }
        await using (var thesis = CreateThesisDbContext())
        {
            thesisAuditCount = await thesis.AuditOutboxMessages.CountAsync();
        }

        using var advisorRead = await GetAdvisorEvaluationAsync(_groupId, advisorId);
        var advisorContent = await advisorRead.Content.ReadAsStringAsync();
        Assert.True(advisorRead.StatusCode == HttpStatusCode.OK, advisorContent);
        using var advisorJson = JsonDocument.Parse(advisorContent);
        var advisorData = advisorJson.RootElement.GetProperty("data");
        Assert.Equal(_groupId, advisorData.GetProperty("groupId").GetGuid());
        Assert.Equal(report.Id, advisorData.GetProperty("finalReportId").GetGuid());
        Assert.Equal(8.35m, advisorData.GetProperty("score").GetDecimal());
        Assert.Equal("Nhận xét chỉ dành cho GVHD.", advisorData.GetProperty("comment").GetString());
        Assert.Equal(savedVersion, advisorData.GetProperty("evaluationVersion").GetUInt32());
        Assert.False(advisorData.GetProperty("isLocked").GetBoolean());

        using var adminRead = await GetAdvisorEvaluationAsync(_groupId, Guid.NewGuid(), "Admin");
        Assert.Equal(HttpStatusCode.OK, adminRead.StatusCode);
        Assert.Equal(advisorContent, await adminRead.Content.ReadAsStringAsync());

        await using (var defense = CreateDefenseDbContext())
        {
            Assert.Equal(defenseAuditCount, await defense.AuditOutboxMessages.CountAsync());
            Assert.Equal(defenseIdempotencyCount, await defense.AdvisorEvaluationIdempotencyRecords.CountAsync());
        }
        await using (var thesis = CreateThesisDbContext())
        {
            Assert.Equal(thesisAuditCount, await thesis.AuditOutboxMessages.CountAsync());
        }
    }

    [Fact]
    public async Task Advisor_evaluation_read_remains_private_after_report_history_access_and_advisor_reassignment()
    {
        var (report, _, originalAdvisorId) = await PrepareEvaluationAsync();
        await SeedIdentityUserAsync(originalAdvisorId, "advisor@test.example", "Advisor");
        using var start = await StartEvaluationAsync(report.Id, 1, originalAdvisorId);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var saved = await SaveAdvisorEvaluationAsync(
            report.Id, 1, null, 9m, "Nhận xét riêng tư.", Guid.NewGuid(), originalAdvisorId);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var studentHistory = new HttpRequestMessage(HttpMethod.Get, $"/api/thesis/groups/{_groupId}/final-report");
        studentHistory.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{_studentId:N}.Student");
        using var history = await _client.SendAsync(studentHistory);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using var studentRead = await GetAdvisorEvaluationAsync(_groupId, _studentId, "Student");
        Assert.Equal(HttpStatusCode.Forbidden, studentRead.StatusCode);
        Assert.Contains("AdvisorEvaluation.AccessDenied", await studentRead.Content.ReadAsStringAsync());
        using var facultyRead = await GetAdvisorEvaluationAsync(_groupId, Guid.NewGuid(), "Faculty");
        Assert.Equal(HttpStatusCode.Forbidden, facultyRead.StatusCode);

        var newAdvisorId = Guid.NewGuid();
        await using (var thesis = CreateThesisDbContext())
        {
            var topicId = (await thesis.StudentGroups.SingleAsync()).TopicId;
            _advisorAssignments.Assignments[topicId] = newAdvisorId;
        }
        using var formerAdvisorRead = await GetAdvisorEvaluationAsync(_groupId, originalAdvisorId);
        Assert.Equal(HttpStatusCode.Forbidden, formerAdvisorRead.StatusCode);
        using var currentAdvisorRead = await GetAdvisorEvaluationAsync(_groupId, newAdvisorId);
        Assert.Equal(HttpStatusCode.OK, currentAdvisorRead.StatusCode);
        Assert.Contains("Nhận xét riêng tư.", await currentAdvisorRead.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Approved_evaluation_exposes_only_a_currently_bound_reviewer_assignment_eligibility_contract()
    {
        var (report, file, advisorId) = await PrepareEvaluationAsync();
        await SeedIdentityUserAsync(advisorId, "advisor@test.example", "Advisor");
        using var start = await StartEvaluationAsync(report.Id, 1, advisorId);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var saved = await SaveAdvisorEvaluationAsync(
            report.Id, 1, null, 8m, "Đủ điều kiện phản biện.", Guid.NewGuid(), advisorId);
        var savedContent = await saved.Content.ReadAsStringAsync();
        Assert.True(saved.StatusCode == HttpStatusCode.OK, savedContent);
        var version = EvaluationVersion(savedContent);

        using (var scope = _app.Services.CreateScope())
        {
            var eligibility = scope.ServiceProvider.GetRequiredService<IReviewerAssignmentEligibilityContract>();
            Assert.Null(await eligibility.GetReviewerAssignmentEligibilityAsync(_groupId));
        }

        _clock.UtcNow = Now.AddMinutes(15);
        using var approved = await DecideAdvisorEvaluationAsync(
            report.Id, 1, version, true, null, Guid.NewGuid(), advisorId);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        using (var scope = _app.Services.CreateScope())
        {
            var eligibility = scope.ServiceProvider.GetRequiredService<IReviewerAssignmentEligibilityContract>();
            var result = await eligibility.GetReviewerAssignmentEligibilityAsync(_groupId);
            Assert.NotNull(result);
            Assert.Equal(_groupId, result.GroupId);
            Assert.Equal(report.Id, result.FinalReportId);
            Assert.Equal(1, result.ReportVersionNo);
            Assert.Equal(file.Id, result.SubmissionFileId);
            Assert.Equal(file.ChecksumSha256, result.SubmissionChecksumSha256);
            Assert.Equal(_clock.UtcNow, result.ApprovedAtUtc);
        }
    }

    private async Task<HttpResponseMessage> GetAdvisorEvaluationAsync(Guid groupId, Guid actorId, string role = "Lecturer")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AdvisorEvaluationReadRoute(groupId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{actorId:N}.{role}");
        return await _client.SendAsync(request);
    }
}
