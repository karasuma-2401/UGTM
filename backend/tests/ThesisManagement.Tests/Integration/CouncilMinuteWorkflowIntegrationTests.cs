using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportWorkflowIntegrationTests
{
    private const string MinutePassword = "Test-only!MinutePassword42";

    private async Task SetMinutePasswordAsync()
    {
        using var scope = _app.Services.CreateScope();
        await using var db = Db<IdentityDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == _adminId);
        user.ChangePassword(scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(user, MinutePassword), _clock.UtcNow);
        await db.SaveChangesAsync();
    }

    private Task<HttpResponseMessage> ChangeMinuteAsync(string operation, string? reason = "Lý do hội đồng xác định phiếu không hợp lệ.", Guid? actor = null)
        => SendAsync(operation == "save" ? HttpMethod.Put : HttpMethod.Post, $"/api/councils/assignments/{_scoreAssignmentId}/minute{(operation == "save" ? "" : "/" + operation)}",
            actor ?? _adminId, "Admin", operation switch
            {
                "save" => JsonContent.Create(new { secretaryMarking = "RejectedAsPerMinute", targetComponentCode = "GVPB", studentFeedback = "Góp ý công khai cho sinh viên.", reason }),
                "lock" => JsonContent.Create(new { password = MinutePassword }),
                _ => JsonContent.Create(new { overrideReason = "Mở khóa để sửa biên bản theo kết luận hội đồng." })
            });

    [Fact]
    public async Task Minute_real_reauthentication_and_exclusion_save_commit_with_separate_reason_and_audit()
    {
        await PrepareScoreCalculationAsync();
        await SetMinutePasswordAsync();
        using (var unlocked = await ChangeMinuteAsync("unlock")) await ArchivalDataAsync(unlocked);
        using (var saved = await ChangeMinuteAsync("save")) await ArchivalDataAsync(saved);
        using (var locked = await ChangeMinuteAsync("lock")) await ArchivalDataAsync(locked);

        await using var verify = Db<DefenseCouncilDbContext>();
        var minute = await verify.CouncilMinutes.SingleAsync();
        Assert.Equal(CouncilMinuteLockStatus.Locked, minute.LockStatus);
        Assert.Equal("Góp ý công khai cho sinh viên.", minute.RequiredRevisions);
        var reviewer = await verify.ReviewerEvaluations.SingleAsync();
        Assert.True(reviewer.IsIgnoredInCalculation);
        Assert.Equal("Lý do hội đồng xác định phiếu không hợp lệ.", reviewer.ExclusionReason);
        var entry = JsonSerializer.Deserialize<AuditIntegrationEvent>((await verify.AuditOutboxMessages
            .SingleAsync(a => a.Action == "DefenseCouncil.CouncilMinute.Saved")).PayloadJson)!;
        using var metadata = JsonDocument.Parse(entry.MetadataJson!);
        Assert.Equal(reviewer.ExclusionReason, metadata.RootElement.GetProperty("Reason").GetString());
        Assert.False(metadata.RootElement.GetProperty("Before").GetProperty("Reviewer").GetProperty("IsIgnoredInCalculation").GetBoolean());
        Assert.True(metadata.RootElement.GetProperty("After").GetProperty("Reviewer").GetProperty("IsIgnoredInCalculation").GetBoolean());
    }

    [Fact]
    public async Task Minute_save_rolls_back_exclusions_and_feedback_when_audit_insert_fails()
    {
        await PrepareScoreCalculationAsync();
        using (var unlocked = await ChangeMinuteAsync("unlock")) await ArchivalDataAsync(unlocked);
        await using (var db = MigrationDb())
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE councils.audit_outbox_message ADD CONSTRAINT test_minute_audit_failure
                CHECK (action <> 'DefenseCouncil.CouncilMinute.Saved');
                """);
        using var response = await ChangeMinuteAsync("save");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.False((await verify.ReviewerEvaluations.SingleAsync()).IsIgnoredInCalculation);
        Assert.NotEqual("Góp ý công khai cho sinh viên.", (await verify.CouncilMinutes.SingleAsync()).RequiredRevisions);
        Assert.False(await verify.AuditOutboxMessages.AnyAsync(a => a.Action == "DefenseCouncil.CouncilMinute.Saved"));
    }

    [Theory]
    [InlineData("save")]
    [InlineData("lock")]
    [InlineData("unlock")]
    public async Task Minute_write_waits_for_publication_and_returns_conflict_without_mutating_published_sources(string operation)
    {
        await PrepareScoreCalculationAsync();
        if (operation == "lock") await SetMinutePasswordAsync();
        using (var calculated = await CalculateAsync()) await ArchivalDataAsync(calculated);
        await using var publisher = MigrationDb();
        await using var transaction = await publisher.Database.BeginTransactionAsync();
        await publisher.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM thesis.student_group WHERE id={_groupId} FOR UPDATE");
        var writing = ChangeMinuteAsync(operation);
        try
        {
            await WaitForScoreSqlLockAsync("student_group");
            await publisher.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE councils.score_summary SET status='PublishedToStudent', published_revision_id=active_revision_id,
                    published_by={_adminId}, published_at={_clock.UtcNow}
                """);
        }
        finally { await transaction.CommitAsync(); }
        using var response = await writing.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Council.ScoresAlreadyPublished", await response.Content.ReadAsStringAsync());
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Equal(CouncilMinuteLockStatus.Locked, (await verify.CouncilMinutes.SingleAsync()).LockStatus);
        Assert.False((await verify.ReviewerEvaluations.SingleAsync()).IsIgnoredInCalculation);
    }

    [Fact]
    public async Task Minute_unlock_does_not_trust_admin_role_from_a_stale_token()
    {
        await PrepareScoreCalculationAsync();
        using var response = await ChangeMinuteAsync("unlock", actor: _scoreReviewerId);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Equal(CouncilMinuteLockStatus.Locked, (await verify.CouncilMinutes.SingleAsync()).LockStatus);
    }
}
