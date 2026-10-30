using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Publish;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Evaluation_cp3_real_workflow_hides_draft_publishes_comment_only_and_uses_new_active_rule_without_mutating_final_grade(bool admin)
    {
        await using (var identity = Db<IdentityDbContext>())
        {
            var lecturer = await identity.Roles.SingleAsync(value => value.RoleName == "Lecturer");
            identity.UserRoles.Add(new UserRole { UserId = _replacementAdvisorId, RoleId = lecturer.Id, AssignedAt = SeedTime }); await identity.SaveChangesAsync();
        }
        using var v1 = await SubmitAsync("pdf", _studentId); using var v2 = await SubmitAsync("docx", _partnerId);
        Assert.Equal(HttpStatusCode.OK, v1.StatusCode); Assert.Equal(HttpStatusCode.OK, v2.StatusCode);
        Guid reportId, fileId;
        await using (var thesis = Db<ThesisManagementDbContext>())
        {
            reportId = (await thesis.FinalReports.SingleAsync()).Id; fileId = (await thesis.SubmissionFiles.SingleAsync(value => value.VersionNo == 2)).Id;
        }
        using var locked = await BeginAsync(reportId, 2, _advisorId); Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        using var advisor = await SaveAdvisorEvaluationAsync(reportId, 2, null, 8.5m, "Nhận xét GVHD riêng tư", Guid.NewGuid(), _advisorId, "Lecturer");
        using var approved = await DecideAdvisorEvaluationAsync(reportId, 2, EvaluationVersion(await advisor.Content.ReadAsStringAsync()), true, null, Guid.NewGuid(), _advisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        _clock.UtcNow = _deadlineAtUtc.AddDays(1);
        using var assigned = await WriteReviewerAsync(HttpMethod.Post, "/api/thesis/reviewer-assignments", new
        {
            groupId = _groupId,
            reviewerId = _replacementAdvisorId,
            expectedAdvisorEvaluationId = (await ReadDataAsync(approved)).GetProperty("id").GetGuid(),
            expectedReportVersionNo = 2
        });
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode); var parent = await ReadDataAsync(assigned);
        var assignmentId = parent.GetProperty("id").GetGuid();
        using var draft = await WriteGradeAsync(null, false); Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var draftData = await ReadDataAsync(draft); await FeedbackAsync(null);
        using (var draftGate = await SendAsync(HttpMethod.Get, $"/api/thesis/groups/{_groupId}/defense-eligibility", _adminId, "Admin"))
            Assert.Equal("EvaluationNotSubmitted", (await ReadDataAsync(draftGate)).GetProperty("reasonCode").GetString());
        using var edited = await WriteGradeAsync(draftData.GetProperty("evaluationVersion").GetUInt32(), false, 5.49m);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode); var editedData = await ReadDataAsync(edited);
        Assert.Equal(draftData.GetProperty("id").GetGuid(), editedData.GetProperty("id").GetGuid());
        Assert.Equal(5.49m, editedData.GetProperty("score").GetDecimal()); await FeedbackAsync(null);
        using var final = await WriteGradeAsync(editedData.GetProperty("evaluationVersion").GetUInt32(), true); Assert.Equal(HttpStatusCode.OK, final.StatusCode);
        var version = (await ReadDataAsync(final)).GetProperty("evaluationVersion").GetUInt32(); await FeedbackAsync("Chỉnh sửa phần thực nghiệm.");
        using (var lockedAgain = await WriteGradeAsync(version, true, 10)) Assert.Equal(HttpStatusCode.Conflict, lockedAgain.StatusCode);
        using (var missing = await SendAsync(HttpMethod.Get, $"/api/thesis/groups/{_groupId}/defense-eligibility", _adminId, "Admin"))
            Assert.Equal("ConfigurationMissing", (await ReadDataAsync(missing)).GetProperty("reasonCode").GetString());
        await PublishAsync(5.5m);
        using (var eligible = await SendAsync(HttpMethod.Get, $"/api/thesis/groups/{_groupId}/defense-eligibility", _adminId, "Admin"))
            Assert.True((await ReadDataAsync(eligible)).GetProperty("canProceed").GetBoolean());
        var currentRule = await PublishAsync(6);
        using (var blocked = await SendAsync(HttpMethod.Get, $"/api/thesis/groups/{_groupId}/defense-eligibility", _adminId, "Admin"))
        {
            var data = await ReadDataAsync(blocked); Assert.False(data.GetProperty("canProceed").GetBoolean());
            Assert.Equal("ScoreBelowMinimum", data.GetProperty("reasonCode").GetString()); Assert.Equal(currentRule, data.GetProperty("scoreConfigurationId").GetGuid());
        }
        await FeedbackAsync("Chỉnh sửa phần thực nghiệm.");
        await AssertDownloadAsync(fileId, Document("docx"), _replacementAdvisorId, "Lecturer");
        using (var privateRead = await SendAsync(HttpMethod.Get, $"/api/thesis/reviewer-assignments/{assignmentId}/evaluation", _studentId, "Student")) Assert.Equal(HttpStatusCode.Forbidden, privateRead.StatusCode);
        using (var advisorRead = await GetAdvisorEvaluationAsync(_replacementAdvisorId, "Lecturer")) Assert.Equal(HttpStatusCode.Forbidden, advisorRead.StatusCode);
        await using var defense = Db<DefenseCouncilDbContext>(); Assert.Equal(version, (await defense.ReviewerEvaluations.SingleAsync()).Version);
        Assert.Equal(3, await defense.ReviewerEvaluationIdempotencyRecords.CountAsync());
        if (!admin) await AssertReviewerOutputsOpenApiAsync();

        async Task<HttpResponseMessage> WriteGradeAsync(uint? expected, bool submit, decimal score = 5.5m)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/thesis/reviewer-evaluations")
            {
                Content = JsonContent.Create(new
                {
                    assignmentId,
                    expectedAssignmentVersion = parent.GetProperty("assignmentVersion").GetUInt32(),
                    expectedEvaluationVersion = expected,
                    action = submit ? "Submit" : "SaveDraft",
                    score = (decimal?)score,
                    comment = "Chỉnh sửa phần thực nghiệm.",
                    defenseApproval = "Approved"
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{(admin ? _adminId : _replacementAdvisorId):N}.{(admin ? "Admin" : "Lecturer")}");
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); return await _client.SendAsync(request);
        }
        async Task FeedbackAsync(string? expected)
        {
            using var response = await SendAsync(HttpMethod.Get, $"/api/thesis/groups/{_groupId}/feedback", _studentId, "Student"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var data = await ReadDataAsync(response); var field = Assert.Single(data.EnumerateObject()); Assert.Equal("comment", field.Name); Assert.Equal(expected, field.Value.GetString());
        }
        async Task<Guid> PublishAsync(decimal minimum)
        {
            Guid id;
            await using (var academic = Db<AcademicPeriodsDbContext>())
            {
                var active = await academic.ScoreWeightConfigs.SingleAsync(value => value.Status == ConfigurationVersionStatus.Active);
                var config = ScoreWeightConfig.CreateDraft(Guid.NewGuid(), active.BatchId, active.VersionNo + 1, ScoreCalculationMethod.IntegerCoefficient,
                    [new("ARBITRARY_REVIEWER", "Reviewer", ScoreComponentSource.Reviewer, ScoreComponentApplicability.Always, 1, true, minimum, true, 1)], _adminId, _clock.UtcNow, "Điều chỉnh ngưỡng cho workflow CP3");
                academic.ScoreWeightConfigs.Add(config); await academic.SaveChangesAsync(); id = config.Id;
            }
            var accessor = _app.Services.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.NameIdentifier, _adminId.ToString()), new Claim(ClaimTypes.Role, "Admin") }, "WorkflowPublisher"))
            };
            try
            {
                await using var scope = _app.Services.CreateAsyncScope(); var academic = scope.ServiceProvider.GetRequiredService<AcademicPeriodsDbContext>();
                var batch = await academic.ScoreWeightConfigs.Where(value => value.Id == id).Select(value => value.BatchId).SingleAsync();
                var result = await scope.ServiceProvider.GetRequiredService<PublishScoreConfigurationHandler>().HandleAsync(new(batch, id));
                Assert.True(result.IsSuccess, result.Error.Code);
            }
            finally { accessor.HttpContext = null; }
            return id;
        }
    }

    private async Task AssertReviewerOutputsOpenApiAsync()
    {
        using var response = await _client.GetAsync("/openapi/v1.json"); var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, content);
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "reviewer-outputs-openapi.json"), content);
        using var json = JsonDocument.Parse(content); var paths = json.RootElement.GetProperty("paths");
        foreach (var path in new[] { "/api/thesis/groups/{groupId}/feedback", "/api/thesis/groups/{groupId}/defense-eligibility" })
        {
            var operation = paths.GetProperty(path).GetProperty("get");
            var parameter = Assert.Single(operation.GetProperty("parameters").EnumerateArray()); Assert.Equal("groupId", parameter.GetProperty("name").GetString());
            Assert.Equal("path", parameter.GetProperty("in").GetString()); Assert.True(parameter.GetProperty("required").GetBoolean());
            Assert.False(operation.TryGetProperty("requestBody", out _));
            foreach (var status in new[] { "200", "400", "401", "403", "404", "409" }) Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        }
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");
        var feedback = schemas.GetProperty("ReviewerFeedbackDto"); Assert.False(feedback.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("comment", Assert.Single(feedback.GetProperty("required").EnumerateArray()).GetString());
        Assert.Equal("comment", Assert.Single(feedback.GetProperty("properties").EnumerateObject()).Name);
        Assert.DoesNotContain("ReviewerEvaluationDto", paths.GetProperty("/api/thesis/groups/{groupId}/feedback").GetRawText());
        var gate = schemas.GetProperty("ReviewerDefenseEligibilityDto").GetProperty("properties"); Assert.Equal(11, gate.EnumerateObject().Count());
        var reasons = gate.GetProperty("reasonCode").GetProperty("enum").EnumerateArray().Select(value => value.GetString()).Where(value => value is not null).Order().ToArray();
        Assert.Equal(Enum.GetNames<ReviewerDefenseEligibilityReason>().Order().ToArray(), reasons);
        foreach (var field in new[] { "score", "comment", "advisorScore", "reviewerScore", "defenseApproval" }) Assert.False(gate.TryGetProperty(field, out _));
    }
}
