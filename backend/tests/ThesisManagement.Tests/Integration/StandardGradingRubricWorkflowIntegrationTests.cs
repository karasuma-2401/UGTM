using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    [Fact]
    public async Task Fixed_rubric_without_configuration_allows_advisor_draft_submit_and_reviewer_approval()
    {
        using var submitted = await SubmitAsync("pdf", _studentId);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Guid reportId;
        await using (var db = Db<ThesisManagementDbContext>()) reportId = (await db.FinalReports.SingleAsync()).Id;
        using var started = await BeginAsync(reportId, 1, _advisorId);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        // No setup call or manual rubric configuration before the lecturer opens the form.
        using var rubricResponse = await SendAsync(HttpMethod.Get,
            $"/api/thesis/batches/{_batchId}/grading-rubric", _advisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.OK, rubricResponse.StatusCode);
        using var rubricJson = JsonDocument.Parse(await rubricResponse.Content.ReadAsStringAsync());
        var criteria = rubricJson.RootElement.GetProperty("data").GetProperty("criteria").EnumerateArray()
            .Select(c => c.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(10, criteria.Length);

        decimal[] marks = [1m, 1m, 1m, 1m, 1m, 1m, 1m, 1m, 0.5m, 0m];
        async Task<HttpResponseMessage> Save(string action, uint? version, int count)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/thesis/advisor-evaluations")
            {
                Content = JsonContent.Create(new
                {
                    finalReportId = reportId,
                    expectedReportVersionNo = 1,
                    expectedEvaluationVersion = version,
                    action,
                    comment = "Đánh giá theo barem chuẩn UIT.",
                    studentScores = new[] { _studentId, _partnerId }.Select(studentId => new
                    {
                        studentId,
                        details = criteria.Take(count).Select((criteriaId, index) => new
                        { criteriaId, score = marks[index], studentId }).ToArray()
                    }).ToArray()
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{_advisorId:N}.Lecturer");
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            return await _client.SendAsync(request);
        }

        using var draft = await Save("SaveDraft", null, 3);
        var draftBody = await draft.Content.ReadAsStringAsync();
        Assert.True(draft.StatusCode == HttpStatusCode.OK, draftBody);
        using var saved = await Save("Submit", EvaluationVersion(draftBody), 10);
        var savedBody = await saved.Content.ReadAsStringAsync();
        Assert.True(saved.StatusCode == HttpStatusCode.OK, savedBody);
        using var approved = await DecideAdvisorEvaluationAsync(reportId, 1, EvaluationVersion(savedBody), true,
            null, Guid.NewGuid(), _advisorId, "Lecturer");
        Assert.True(approved.StatusCode == HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());

        await using var thesis = Db<ThesisManagementDbContext>();
        Assert.Equal(StudentGroupStatus.ReadyForReviewerAssignment, (await thesis.StudentGroups.SingleAsync()).Status);
        await using var defense = Db<DefenseCouncilDbContext>();
        var evaluation = await defense.AdvisorEvaluations.SingleAsync();
        Assert.True(evaluation.IsLocked);
        Assert.Equal(8.5m, evaluation.Score);
        Assert.Equal(20, await defense.EvaluationDetails.CountAsync());
        Assert.Equal(10, await defense.GradingCriterias.CountAsync(c => c.BatchId == _batchId));
    }
}
