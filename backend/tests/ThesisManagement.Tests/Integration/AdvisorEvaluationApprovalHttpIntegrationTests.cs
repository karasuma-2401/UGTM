using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportSubmissionHttpIntegrationTests
{
    private static string AdvisorApprovalRoute(Guid finalReportId)
        => $"/api/thesis/final-reports/{finalReportId}/approval";

    [Fact]
    public async Task Advisor_evaluation_can_be_reconsidered_then_approved_once_with_atomic_group_transition_and_replay()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        await SeedIdentityUserAsync(advisorId, "advisor@test.example", "Advisor");
        using var start = await StartEvaluationAsync(report.Id, 1, advisorId);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var saved = await SaveAdvisorEvaluationAsync(
            report.Id, 1, null, 8m, "Báo cáo đủ chất lượng.", Guid.NewGuid(), advisorId);
        var savedContent = await saved.Content.ReadAsStringAsync();
        Assert.True(saved.StatusCode == HttpStatusCode.OK, savedContent);
        var savedVersion = EvaluationVersion(savedContent);

        _clock.UtcNow = Now.AddMinutes(30);
        using var rejected = await DecideAdvisorEvaluationAsync(
            report.Id, 1, savedVersion, false, "  Cần hoàn tất chỉnh sửa theo góp ý.  ", Guid.NewGuid(), advisorId);
        var rejectedContent = await rejected.Content.ReadAsStringAsync();
        Assert.True(rejected.StatusCode == HttpStatusCode.OK, rejectedContent);
        using var rejectedJson = JsonDocument.Parse(rejectedContent);
        var rejectedData = rejectedJson.RootElement.GetProperty("data");
        Assert.False(rejectedData.GetProperty("allowsReviewerAssignment").GetBoolean());
        Assert.Equal("Cần hoàn tất chỉnh sửa theo góp ý.", rejectedData.GetProperty("rejectionReason").GetString());
        Assert.False(rejectedData.GetProperty("isLocked").GetBoolean());
        var rejectedVersion = rejectedData.GetProperty("evaluationVersion").GetUInt32();
        Assert.True(rejectedVersion > savedVersion);

        var approvalKey = Guid.NewGuid();
        _clock.UtcNow = Now.AddMinutes(45);
        using var approved = await DecideAdvisorEvaluationAsync(
            report.Id, 1, rejectedVersion, true, null, approvalKey, advisorId);
        var approvedContent = await approved.Content.ReadAsStringAsync();
        Assert.True(approved.StatusCode == HttpStatusCode.OK, approvedContent);
        using var approvedJson = JsonDocument.Parse(approvedContent);
        var approvedData = approvedJson.RootElement.GetProperty("data");
        Assert.True(approvedData.GetProperty("allowsReviewerAssignment").GetBoolean());
        Assert.Equal(JsonValueKind.Null, approvedData.GetProperty("rejectionReason").ValueKind);
        Assert.True(approvedData.GetProperty("isLocked").GetBoolean());
        var approvedVersion = approvedData.GetProperty("evaluationVersion").GetUInt32();
        Assert.True(approvedVersion > rejectedVersion);

        using var replay = await DecideAdvisorEvaluationAsync(
            report.Id, 1, rejectedVersion, true, null, approvalKey, advisorId);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(approvedContent, await replay.Content.ReadAsStringAsync());
        using var reusedKey = await DecideAdvisorEvaluationAsync(
            report.Id, 1, rejectedVersion, false, "Nội dung khác", approvalKey, advisorId);
        Assert.Equal(HttpStatusCode.Conflict, reusedKey.StatusCode);
        Assert.Contains("AdvisorEvaluation.IdempotencyKeyReused", await reusedKey.Content.ReadAsStringAsync());

        using var saveAfterApproval = await SaveAdvisorEvaluationAsync(
            report.Id, 1, approvedVersion, 9m, "Không được ghi đè", Guid.NewGuid(), advisorId);
        Assert.Equal(HttpStatusCode.Conflict, saveAfterApproval.StatusCode);
        Assert.Contains("AdvisorEvaluation.Locked", await saveAfterApproval.Content.ReadAsStringAsync());
        using var revoke = await DecideAdvisorEvaluationAsync(
            report.Id, 1, approvedVersion, false, "Không được thu hồi", Guid.NewGuid(), advisorId);
        Assert.Equal(HttpStatusCode.Conflict, revoke.StatusCode);
        Assert.Contains("AdvisorEvaluation.Locked", await revoke.Content.ReadAsStringAsync());

        await using var defense = CreateDefenseDbContext();
        var evaluation = await defense.AdvisorEvaluations.SingleAsync();
        Assert.True(evaluation.AllowsReviewerAssignment);
        Assert.True(evaluation.IsLocked);
        Assert.Equal(advisorId, evaluation.DecisionBy);
        Assert.Equal(_clock.UtcNow, evaluation.DecisionAt);
        var rejectedAudit = await defense.AuditOutboxMessages.SingleAsync(value =>
            value.Action == IntegrationAuditActions.AdvisorEvaluationRejected);
        Assert.DoesNotContain("Cần hoàn tất chỉnh sửa theo góp ý.", rejectedAudit.PayloadJson);
        var rejectedEvent = JsonSerializer.Deserialize<AuditIntegrationEvent>(rejectedAudit.PayloadJson)!;
        using (var rejectedMetadata = JsonDocument.Parse(rejectedEvent.MetadataJson!))
        {
            Assert.True(rejectedMetadata.RootElement.GetProperty("HasRejectionReason").GetBoolean());
        }
        var approvedAudit = await defense.AuditOutboxMessages.SingleAsync(value =>
            value.Action == IntegrationAuditActions.AdvisorEvaluationApproved);
        var approvedEvent = JsonSerializer.Deserialize<AuditIntegrationEvent>(approvedAudit.PayloadJson)!;
        Assert.Equal(advisorId, approvedEvent.ActorUserId);
        Assert.Equal(evaluation.Id.ToString(), approvedEvent.TargetId);

        await using var thesis = CreateThesisDbContext();
        Assert.Equal(StudentGroupStatus.ReadyForReviewerAssignment,
            (await thesis.StudentGroups.SingleAsync()).Status);
        var rejectedThesisAudit = await thesis.AuditOutboxMessages.SingleAsync(value =>
            value.Action == IntegrationAuditActions.AdvisorEvaluationReviewerAssignmentRejected);
        Assert.DoesNotContain("Cần hoàn tất chỉnh sửa theo góp ý.", rejectedThesisAudit.PayloadJson);
        Assert.Single(await thesis.AuditOutboxMessages.Where(value =>
            value.Action == IntegrationAuditActions.AdvisorEvaluationReviewerAssignmentApproved).ToListAsync());
    }

    [Fact]
    public async Task Advisor_evaluation_decision_validates_input_authority_and_the_current_evaluation_version()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        await SeedIdentityUserAsync(advisorId, "advisor@test.example", "Advisor");
        using var start = await StartEvaluationAsync(report.Id, 1, advisorId);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        using var missingEvaluation = await DecideAdvisorEvaluationAsync(
            report.Id, 1, 1, true, null, Guid.NewGuid(), advisorId);
        Assert.Equal(HttpStatusCode.NotFound, missingEvaluation.StatusCode);
        Assert.Contains("AdvisorEvaluation.NotFound", await missingEvaluation.Content.ReadAsStringAsync());

        using var saved = await SaveAdvisorEvaluationAsync(
            report.Id, 1, null, 8m, "Nhận xét", Guid.NewGuid(), advisorId);
        var savedContent = await saved.Content.ReadAsStringAsync();
        Assert.True(saved.StatusCode == HttpStatusCode.OK, savedContent);
        var version = EvaluationVersion(savedContent);

        using var missingReason = await DecideAdvisorEvaluationAsync(
            report.Id, 1, version, false, null, Guid.NewGuid(), advisorId);
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        Assert.Contains("AdvisorEvaluation.InvalidDecision", await missingReason.Content.ReadAsStringAsync());
        using var approvalWithReason = await DecideAdvisorEvaluationAsync(
            report.Id, 1, version, true, "Không hợp lệ", Guid.NewGuid(), advisorId);
        Assert.Equal(HttpStatusCode.BadRequest, approvalWithReason.StatusCode);
        Assert.Contains("AdvisorEvaluation.InvalidDecision", await approvalWithReason.Content.ReadAsStringAsync());
        using var student = await DecideAdvisorEvaluationAsync(
            report.Id, 1, version, true, null, Guid.NewGuid(), _studentId, "Student");
        Assert.Equal(HttpStatusCode.Forbidden, student.StatusCode);
        Assert.Contains("AdvisorEvaluation.AccessDenied", await student.Content.ReadAsStringAsync());
        using var stale = await DecideAdvisorEvaluationAsync(
            report.Id, 1, version + 1, true, null, Guid.NewGuid(), advisorId);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("AdvisorEvaluation.VersionConflict", await stale.Content.ReadAsStringAsync());

        await using var defense = CreateDefenseDbContext();
        var evaluation = await defense.AdvisorEvaluations.SingleAsync();
        Assert.Null(evaluation.AllowsReviewerAssignment);
        Assert.Empty(await defense.AuditOutboxMessages.Where(value =>
            value.Action == IntegrationAuditActions.AdvisorEvaluationApproved
            || value.Action == IntegrationAuditActions.AdvisorEvaluationRejected).ToListAsync());
        await using var thesis = CreateThesisDbContext();
        Assert.Equal(StudentGroupStatus.FinalReportSubmitted, (await thesis.StudentGroups.SingleAsync()).Status);
        Assert.Empty(await thesis.AuditOutboxMessages.Where(value =>
            value.Action == IntegrationAuditActions.AdvisorEvaluationReviewerAssignmentApproved
            || value.Action == IntegrationAuditActions.AdvisorEvaluationReviewerAssignmentRejected).ToListAsync());
    }

    [Fact]
    public async Task Advisor_evaluation_approval_rolls_back_decision_group_transition_and_both_decision_audits_when_thesis_audit_fails()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        await SeedIdentityUserAsync(advisorId, "advisor@test.example", "Advisor");
        using var start = await StartEvaluationAsync(report.Id, 1, advisorId);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var saved = await SaveAdvisorEvaluationAsync(
            report.Id, 1, null, 8m, "Nhận xét", Guid.NewGuid(), advisorId);
        var savedContent = await saved.Content.ReadAsStringAsync();
        Assert.True(saved.StatusCode == HttpStatusCode.OK, savedContent);
        var version = EvaluationVersion(savedContent);

        await using var thesis = CreateThesisDbContext();
        await thesis.Database.ExecuteSqlRawAsync("""
            ALTER TABLE thesis.audit_outbox_message
            ADD CONSTRAINT force_advisor_approval_thesis_audit_failure
            CHECK (action <> 'ThesisManagement.AdvisorEvaluation.ReviewerAssignmentApproved')
            """);
        try
        {
            using var response = await DecideAdvisorEvaluationAsync(
                report.Id, 1, version, true, null, Guid.NewGuid(), advisorId);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            await using var defense = CreateDefenseDbContext();
            var evaluation = await defense.AdvisorEvaluations.SingleAsync();
            Assert.Null(evaluation.AllowsReviewerAssignment);
            Assert.Equal(version, evaluation.Version);
            Assert.Single(await defense.AdvisorEvaluationIdempotencyRecords.ToListAsync());
            Assert.Empty(await defense.AuditOutboxMessages.Where(value =>
                value.Action == IntegrationAuditActions.AdvisorEvaluationApproved).ToListAsync());
            await using var verificationThesis = CreateThesisDbContext();
            Assert.Equal(StudentGroupStatus.FinalReportSubmitted,
                (await verificationThesis.StudentGroups.SingleAsync()).Status);
            Assert.Empty(await verificationThesis.AuditOutboxMessages.Where(value =>
                value.Action == IntegrationAuditActions.AdvisorEvaluationReviewerAssignmentApproved).ToListAsync());
        }
        finally
        {
            await thesis.Database.ExecuteSqlRawAsync(
                "ALTER TABLE thesis.audit_outbox_message DROP CONSTRAINT force_advisor_approval_thesis_audit_failure");
        }
    }

    private async Task<HttpResponseMessage> DecideAdvisorEvaluationAsync(
        Guid finalReportId,
        int expectedReportVersionNo,
        uint expectedEvaluationVersion,
        bool allowsReviewerAssignment,
        string? rejectionReason,
        Guid idempotencyKey,
        Guid actorId,
        string role = "Lecturer")
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, AdvisorApprovalRoute(finalReportId))
        {
            Content = JsonContent.Create(new
            {
                expectedReportVersionNo,
                expectedEvaluationVersion,
                allowsReviewerAssignment,
                rejectionReason
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{actorId:N}.{role}");
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        return await _client.SendAsync(request);
    }

    private static uint EvaluationVersion(string responseContent)
    {
        using var json = JsonDocument.Parse(responseContent);
        return json.RootElement.GetProperty("data").GetProperty("evaluationVersion").GetUInt32();
    }
}
