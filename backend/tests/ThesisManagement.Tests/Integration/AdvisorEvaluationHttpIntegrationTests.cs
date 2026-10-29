using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using IdentityUser = UGTM.Modules.Identity.Domain.Entities.User;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportSubmissionHttpIntegrationTests
{
    private static string AdvisorEvaluationRoute => "/api/thesis/advisor-evaluations";

    [Fact]
    public async Task Advisor_evaluation_saves_the_locked_report_binding_rounds_score_and_replays_the_original_response()
    {
        var (report, file, advisorId) = await PrepareEvaluationAsync();
        await SeedIdentityUserAsync(advisorId, "advisor@test.example", "Advisor");
        using var start = await StartEvaluationAsync(report.Id, 1, advisorId);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        _clock.UtcNow = Now.AddMinutes(15);
        var key = Guid.NewGuid();

        using var first = await SaveAdvisorEvaluationAsync(report.Id, 1, null, 8.345m, "  Báo cáo đạt yêu cầu.  ", key, advisorId);
        var firstContent = await first.Content.ReadAsStringAsync();
        Assert.True(first.StatusCode == HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        using var firstJson = JsonDocument.Parse(firstContent);
        var data = firstJson.RootElement.GetProperty("data");
        Assert.Equal(_groupId, data.GetProperty("groupId").GetGuid());
        Assert.Equal(report.Id, data.GetProperty("finalReportId").GetGuid());
        Assert.Equal(file.Id, data.GetProperty("submissionFileId").GetGuid());
        Assert.Equal(1, data.GetProperty("reportVersionNo").GetInt32());
        Assert.Equal(8.35m, data.GetProperty("score").GetDecimal());
        Assert.Equal("Báo cáo đạt yêu cầu.", data.GetProperty("comment").GetString());
        Assert.Equal(advisorId, data.GetProperty("evaluatedBy").GetGuid());
        Assert.Equal(_clock.UtcNow, data.GetProperty("evaluatedAtUtc").GetDateTime());
        Assert.True(data.GetProperty("evaluationVersion").GetUInt32() > 0);
        Assert.False(data.GetProperty("isLocked").GetBoolean());

        using var replay = await SaveAdvisorEvaluationAsync(report.Id, 1, null, 8.345m, "  Báo cáo đạt yêu cầu.  ", key, advisorId);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(firstContent, await replay.Content.ReadAsStringAsync());
        using var reusedKey = await SaveAdvisorEvaluationAsync(report.Id, 1, null, 7m, "Nội dung khác", key, advisorId);
        Assert.Equal(HttpStatusCode.Conflict, reusedKey.StatusCode);
        Assert.Contains("AdvisorEvaluation.IdempotencyKeyReused", await reusedKey.Content.ReadAsStringAsync());

        await using var defense = CreateDefenseDbContext();
        var evaluation = await defense.AdvisorEvaluations.SingleAsync();
        Assert.Equal(8.35m, evaluation.Score);
        Assert.Equal("Báo cáo đạt yêu cầu.", evaluation.Comment);
        Assert.Equal(advisorId, evaluation.EvaluatedBy);
        Assert.Single(await defense.AdvisorEvaluationIdempotencyRecords.ToListAsync());
        Assert.Single(await defense.AuditOutboxMessages.Where(value =>
            value.Action == IntegrationAuditActions.AdvisorEvaluationSaved).ToListAsync());
        await using var thesis = CreateThesisDbContext();
        Assert.Equal(UGTM.Modules.ThesisManagement.Domain.Enums.FinalReportStatus.UnderEvaluation,
            (await thesis.FinalReports.SingleAsync()).Status);
        Assert.Equal(UGTM.Modules.ThesisManagement.Domain.Enums.StudentGroupStatus.FinalReportSubmitted,
            (await thesis.StudentGroups.SingleAsync()).Status);
    }

    [Fact]
    public async Task Advisor_evaluation_requires_the_current_advisor_or_administrator_and_rejects_invalid_input_without_mutation()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        await SeedIdentityUserAsync(advisorId, "advisor@test.example", "Advisor");
        using var start = await StartEvaluationAsync(report.Id, 1, advisorId);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        using var studentAttempt = await SaveAdvisorEvaluationAsync(report.Id, 1, null, 8m, "Nhận xét", Guid.NewGuid(), _studentId, "Student");
        Assert.Equal(HttpStatusCode.Forbidden, studentAttempt.StatusCode);
        Assert.Contains("AdvisorEvaluation.AccessDenied", await studentAttempt.Content.ReadAsStringAsync());
        using var invalidScore = await SaveAdvisorEvaluationAsync(report.Id, 1, null, 10.004m, "Nhận xét", Guid.NewGuid(), advisorId);
        Assert.Equal(HttpStatusCode.BadRequest, invalidScore.StatusCode);
        Assert.Contains("AdvisorEvaluation.InvalidScore", await invalidScore.Content.ReadAsStringAsync());

        await using var defense = CreateDefenseDbContext();
        Assert.Empty(await defense.AdvisorEvaluations.ToListAsync());
        Assert.Empty(await defense.AdvisorEvaluationIdempotencyRecords.ToListAsync());
        Assert.Empty(await defense.AuditOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Advisor_evaluation_updates_only_with_the_current_evaluation_version()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        await SeedIdentityUserAsync(advisorId, "advisor@test.example", "Advisor");
        using var start = await StartEvaluationAsync(report.Id, 1, advisorId);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var created = await SaveAdvisorEvaluationAsync(report.Id, 1, null, 7.5m, "Bản đầu", Guid.NewGuid(), advisorId);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.True(created.StatusCode == HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var version = createdJson.RootElement.GetProperty("data").GetProperty("evaluationVersion").GetUInt32();
        _clock.UtcNow = Now.AddMinutes(30);

        using var stale = await SaveAdvisorEvaluationAsync(report.Id, 1, version + 1, 9m, "Bản lỗi thời", Guid.NewGuid(), advisorId);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("AdvisorEvaluation.VersionConflict", await stale.Content.ReadAsStringAsync());
        using var updated = await SaveAdvisorEvaluationAsync(report.Id, 1, version, 9m, "Bản đã sửa", Guid.NewGuid(), advisorId);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedJson = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal(9m, updatedJson.RootElement.GetProperty("data").GetProperty("score").GetDecimal());
        Assert.True(updatedJson.RootElement.GetProperty("data").GetProperty("evaluationVersion").GetUInt32() > version);

        await using var defense = CreateDefenseDbContext();
        var evaluation = await defense.AdvisorEvaluations.SingleAsync();
        Assert.Equal(9m, evaluation.Score);
        Assert.Equal("Bản đã sửa", evaluation.Comment);
        Assert.Equal(2, await defense.AuditOutboxMessages.CountAsync(value =>
            value.Action == IntegrationAuditActions.AdvisorEvaluationSaved));
    }

    [Fact]
    public async Task Advisor_evaluation_rolls_back_the_evaluation_and_idempotency_record_when_its_audit_cannot_be_written()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        await SeedIdentityUserAsync(advisorId, "advisor@test.example", "Advisor");
        using var start = await StartEvaluationAsync(report.Id, 1, advisorId);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        await using var defense = CreateDefenseDbContext();
        await defense.Database.ExecuteSqlRawAsync("""
            ALTER TABLE councils.audit_outbox_message
            ADD CONSTRAINT force_advisor_evaluation_audit_failure
            CHECK (action <> 'DefenseCouncil.AdvisorEvaluation.Saved')
            """);

        using var response = await SaveAdvisorEvaluationAsync(report.Id, 1, null, 8m, "Nhận xét", Guid.NewGuid(), advisorId);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await defense.AdvisorEvaluations.ToListAsync());
        Assert.Empty(await defense.AdvisorEvaluationIdempotencyRecords.ToListAsync());
        await defense.Database.ExecuteSqlRawAsync(
            "ALTER TABLE councils.audit_outbox_message DROP CONSTRAINT force_advisor_evaluation_audit_failure");
    }

    private async Task<HttpResponseMessage> SaveAdvisorEvaluationAsync(
        Guid finalReportId,
        int expectedReportVersionNo,
        uint? expectedEvaluationVersion,
        decimal score,
        string? comment,
        Guid idempotencyKey,
        Guid actorId,
        string role = "Lecturer")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, AdvisorEvaluationRoute)
        {
            Content = JsonContent.Create(new
            {
                finalReportId,
                expectedReportVersionNo,
                expectedEvaluationVersion,
                score,
                comment
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{actorId:N}.{role}");
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        return await _client.SendAsync(request);
    }

    private async Task SeedIdentityUserAsync(Guid userId, string email, string fullName)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        await using var identity = new IdentityDbContext(options.Options);
        identity.Users.Add(IdentityUser.Create(userId, email, email.ToUpperInvariant(), fullName,
            null, null, null, AccountStatus.Active, Now));
        await identity.SaveChangesAsync();
    }
}
