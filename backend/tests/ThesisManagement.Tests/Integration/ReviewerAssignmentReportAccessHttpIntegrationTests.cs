using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Database.Migrations;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    private static string ReviewerDownloadRoute(Guid id) => $"/api/thesis/final-report-files/{id}/download";

    [Fact]
    public async Task Reviewer_reads_me_shows_only_owned_active_tasks_exact_safe_file_metadata_and_no_scores_or_urls()
    {
        var original = await CreateForCancellationAsync();
        using (var own = await ReadPageAsync(TasksRoute, _reviewer, "Lecturer"))
        {
            var item = Assert.Single(Items(own));
            Assert.Equal(original.Id, item.GetProperty("id").GetGuid());
            Assert.True(item.GetProperty("canDownloadReport").GetBoolean());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("accessBlockCode").ValueKind);
            var file = item.GetProperty("reportFile");
            Assert.Equal(_file, file.GetProperty("fileId").GetGuid());
            Assert.Equal("report.pdf", file.GetProperty("fileName").GetString());
            Assert.Equal("application/pdf", file.GetProperty("contentType").GetString());
            Assert.Equal(100, file.GetProperty("sizeBytes").GetInt64());
            Assert.Equal(2, file.GetProperty("versionNo").GetInt32());
            foreach (var field in new[] { "score", "comment", "reviewerId", "advisorEvaluationId", "cancellationReason", "cancelledBy", "assignedBy", "objectKey", "downloadUrl" })
                Assert.False(item.TryGetProperty(field, out _));
            Assert.DoesNotContain("objectKey", item.GetRawText());
            Assert.DoesNotContain("downloadUrl", item.GetRawText());
        }
        using (var other = await ReadPageAsync(TasksRoute, _otherReviewer, "Lecturer")) Assert.Empty(Items(other));
        using (var keyword = await ReadPageAsync(TasksRoute + "?keyword=Lecturer", _reviewer, "Lecturer")) Assert.Empty(Items(keyword));
        using (var otherBatch = await ReadPageAsync($"{TasksRoute}?batchId={Guid.NewGuid()}", _reviewer, "Lecturer")) Assert.Empty(Items(otherBatch));
        using (var history = await ReadAsync($"/api/thesis/groups/{_group}/final-report", _reviewer, "Lecturer")) Assert.Equal(HttpStatusCode.Forbidden, history.StatusCode);
        using (var evaluation = await ReadAsync($"/api/thesis/groups/{_group}/advisor-evaluation", _reviewer, "Lecturer")) Assert.Equal(HttpStatusCode.Forbidden, evaluation.StatusCode);
        Assert.Empty(_reviewerStorage.Downloads);
        await AssertCountsAsync(1, 1, 1);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("role")]
    public async Task Reviewer_reads_revoked_lecturer_grant_or_suspended_account_denies_me_and_new_download_even_with_stale_claims(string change)
    {
        await CreateForCancellationAsync();
        await using (var writer = Db<UGTMDbContext>()) await writer.Database.ExecuteSqlInterpolatedAsync(Change(change));
        using (var tasks = await ReadAsync(TasksRoute, _reviewer, "Lecturer"))
            await AssertErrorAsync(tasks, HttpStatusCode.Forbidden, "AccessDenied");
        using (var download = await ReadAsync(ReviewerDownloadRoute(_file), _reviewer, "Lecturer")) Assert.Equal(HttpStatusCode.Forbidden, download.StatusCode);
        Assert.Empty(_reviewerStorage.Downloads);
        await AssertCountsAsync(1, 1, 1);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Student")]
    [InlineData("CouncilMember")]
    public async Task Reviewer_reads_me_does_not_bypass_database_lecturer_membership_for_other_roles(string role)
    {
        using var tasks = await ReadAsync(TasksRoute, _actor, role);
        await AssertErrorAsync(tasks, HttpStatusCode.Forbidden, "AccessDenied");
        await AssertCountsAsync(0, 0, 0);
    }

    [Theory]
    [InlineData("legacy", "LegacyRecordNeedsResolution")]
    [InlineData("context", "ContextInvalid")]
    [InlineData("advisor", "AdvisorConflict")]
    [InlineData("binding", "BindingInvalid")]
    [InlineData("group", "BindingInvalid")]
    [InlineData("approval", "BindingInvalid")]
    [InlineData("report", "BindingInvalid")]
    [InlineData("snapshot", "BindingInvalid")]
    public async Task Reviewer_reads_keeps_blocked_active_tasks_visible_with_priority_codes_and_hides_file_metadata(string change, string block)
    {
        var original = await CreateForCancellationAsync();
        await using (var writer = Db<UGTMDbContext>())
        {
            if (change == "legacy")
            {
                await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE councils.reviewer_assignment SET is_legacy = true WHERE id = {original.Id}");
                await writer.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM thesis.student_group_member WHERE group_id = {_group}");
            }
            else if (change == "context")
            {
                await writer.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM thesis.student_group_member WHERE group_id = {_group}");
                await writer.Database.ExecuteSqlInterpolatedAsync(Change("advisor"));
            }
            else if (change == "approval") await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE councils.advisor_evaluation SET allows_reviewer_assignment = NULL, decision_by = NULL, decision_at = NULL WHERE group_id = {_group}");
            else if (change == "report") await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.final_report SET status = 'Submitted' WHERE id = {_report}");
            else if (change == "snapshot") await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE councils.reviewer_assignment SET submission_checksum_sha256 = {new string('e', 64)} WHERE id = {original.Id}");
            else await writer.Database.ExecuteSqlInterpolatedAsync(Change(change));
        }
        using var tasks = await ReadPageAsync(TasksRoute, _reviewer, "Lecturer");
        var item = Assert.Single(Items(tasks));
        Assert.Equal(original.Id, item.GetProperty("id").GetGuid());
        Assert.False(item.GetProperty("canDownloadReport").GetBoolean());
        Assert.Equal(block, item.GetProperty("accessBlockCode").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("reportFile").ValueKind);
        // A reviewer who becomes the advisor still has the independent SCRUM-102 advisor permission.
        if (change is not ("advisor" or "context"))
        {
            using var download = await ReadAsync(ReviewerDownloadRoute(_file), _reviewer, "Lecturer");
            Assert.Equal(HttpStatusCode.Forbidden, download.StatusCode);
        }
        Assert.Empty(_reviewerStorage.Downloads);
        await AssertCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Reviewer_reads_downloads_only_approved_v2_with_five_minute_ttl_and_cancellation_stops_new_urls()
    {
        var original = await CreateForCancellationAsync();
        var before = DateTime.UtcNow;
        using (var download = await ReadAsync(ReviewerDownloadRoute(_file), _reviewer, "Lecturer"))
        {
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            using var json = JsonDocument.Parse(await download.Content.ReadAsStringAsync());
            var data = json.RootElement.GetProperty("data");
            Assert.Equal(_file, data.GetProperty("fileId").GetGuid());
            Assert.InRange(data.GetProperty("expiresAtUtc").GetDateTime(), before.AddMinutes(5), DateTime.UtcNow.AddMinutes(5));
            Assert.False(data.TryGetProperty("objectKey", out _));
        }
        Assert.Equal(TimeSpan.FromMinutes(5), Assert.Single(_reviewerStorage.Downloads).Lifetime);
        Assert.Equal($"cp2/{_file:N}", _reviewerStorage.Downloads[0].ObjectKey);
        foreach (var id in new[] { _oldFile, (await SeedReadGroupAsync("Another report", 1)).FileId })
        {
            using var denied = await ReadAsync(ReviewerDownloadRoute(id), _reviewer, "Lecturer");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using (var cancel = await DeleteAsync(original.Id, original.Version)) Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        using (var gone = await ReadPageAsync(TasksRoute, _reviewer, "Lecturer")) Assert.Empty(Items(gone));
        using (var denied = await ReadAsync(ReviewerDownloadRoute(_file), _reviewer, "Lecturer")) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Single(_reviewerStorage.Downloads);
        using var reassigned = await PostAsync(await BodyAsync(_otherReviewer));
        Assert.Equal(HttpStatusCode.Created, reassigned.StatusCode);
        using (var oldReviewer = await ReadAsync(ReviewerDownloadRoute(_file), _reviewer, "Lecturer")) Assert.Equal(HttpStatusCode.Forbidden, oldReviewer.StatusCode);
        using (var newReviewer = await ReadAsync(ReviewerDownloadRoute(_file), _otherReviewer, "Lecturer")) Assert.Equal(HttpStatusCode.OK, newReviewer.StatusCode);
        Assert.Equal(2, _reviewerStorage.Downloads.Count);
        await AssertCountsAsync(2, 3, 3);
    }

    [Fact]
    public async Task Reviewer_reads_closed_batch_allows_own_tasks_and_download_without_student_deadline_guards()
    {
        await CreateForCancellationAsync();
        await using (var writer = Db<UGTMDbContext>()) await writer.Database.ExecuteSqlInterpolatedAsync(Change("batch"));
        using (var tasks = await ReadPageAsync(TasksRoute, _reviewer, "Lecturer")) Assert.True(Assert.Single(Items(tasks)).GetProperty("canDownloadReport").GetBoolean());
        using var download = await ReadAsync(ReviewerDownloadRoute(_file), _reviewer, "Lecturer");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Single(_reviewerStorage.Downloads);
        await AssertCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Reviewer_reads_existing_student_advisor_admin_permissions_survive_cancellation_and_unrelated_users_stay_denied()
    {
        var original = await CreateForCancellationAsync();
        using var cancel = await DeleteAsync(original.Id, original.Version);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        foreach (var (actor, role) in new[] { (_student, "Student"), (_advisor, "Lecturer"), (_actor, "Admin") })
        {
            using var old = await ReadAsync(ReviewerDownloadRoute(_oldFile), actor, role);
            Assert.Equal(HttpStatusCode.OK, old.StatusCode);
            using var current = await ReadAsync(ReviewerDownloadRoute(_file), actor, role);
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        }
        foreach (var (actor, role) in new[] { (_otherReviewer, "Lecturer"), (_otherReviewer, "CouncilMember"), (_actor, "Student"), (_reviewer, "Lecturer") })
        {
            using var denied = await ReadAsync(ReviewerDownloadRoute(_file), actor, role);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        Assert.Equal(6, _reviewerStorage.Downloads.Count);
        await AssertCountsAsync(1, 2, 2);
    }

    [Fact]
    public async Task Reviewer_reads_download_keeps_not_found_and_wrong_entity_semantics_before_issuing_urls()
    {
        await CreateForCancellationAsync();
        using (var missing = await ReadAsync(ReviewerDownloadRoute(Guid.NewGuid()), _reviewer, "Lecturer")) Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await using (var writer = Db<UGTMDbContext>())
            await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.submission_file SET entity_type = {nameof(SubmissionEntityType.Syllabus)} WHERE id = {_file}");
        using var wrong = await ReadAsync(ReviewerDownloadRoute(_file), _reviewer, "Lecturer");
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        Assert.Empty(_reviewerStorage.Downloads);
    }
}
