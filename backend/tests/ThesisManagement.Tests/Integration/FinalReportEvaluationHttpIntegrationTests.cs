using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportSubmissionHttpIntegrationTests
{
    private static string EvaluationRoute(Guid reportId) => $"/api/thesis/final-reports/{reportId}/begin-evaluation";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Beginning_evaluation_locks_the_reviewed_file_and_audits_the_actor_atomically(bool administrator)
    {
        var (initial, _, advisorId) = await PrepareEvaluationAsync();
        using var revision = await SubmitVersionAsync(2);
        Assert.Equal(HttpStatusCode.OK, revision.StatusCode);
        _clock.UtcNow = Now.AddMinutes(10);
        var actorId = administrator ? Guid.NewGuid() : advisorId;

        using var response = await StartEvaluationAsync(initial.Id, 2, actorId, administrator ? "aDmIn" : "Lecturer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(initial.Id, data.GetProperty("finalReportId").GetGuid());
        Assert.Equal(_groupId, data.GetProperty("groupId").GetGuid());
        Assert.Equal("InitialFinal", data.GetProperty("versionType").GetString());
        Assert.Equal(2, data.GetProperty("versionNo").GetInt32());
        Assert.Equal("UnderEvaluation", data.GetProperty("status").GetString());
        Assert.Equal(actorId, data.GetProperty("startedBy").GetGuid());
        Assert.Equal(_clock.UtcNow, data.GetProperty("startedAtUtc").GetDateTime());
        await using var db = CreateThesisDbContext();
        var report = await db.FinalReports.SingleAsync();
        var files = await db.SubmissionFiles.OrderBy(file => file.VersionNo).ToListAsync();
        var lockedFile = files[1];
        Assert.Equal(FinalReportStatus.UnderEvaluation, report.Status);
        Assert.Equal(2, report.CurrentVersionNo);
        Assert.Equal(Now, report.SubmittedAt);
        Assert.Equal(lockedFile.Id, data.GetProperty("file").GetProperty("id").GetGuid());
        Assert.Equal(lockedFile.ChecksumSha256, data.GetProperty("file").GetProperty("checksumSha256").GetString());
        Assert.False(data.GetProperty("file").TryGetProperty("objectKey", out _));
        Assert.Equal(StudentGroupStatus.FinalReportSubmitted, (await db.StudentGroups.SingleAsync()).Status);
        Assert.Equal(3, await db.AuditOutboxMessages.CountAsync());
        var audit = await db.AuditOutboxMessages.SingleAsync(row => row.Action == IntegrationAuditActions.FinalReportEvaluationStarted);
        var entry = JsonSerializer.Deserialize<AuditIntegrationEvent>(audit.PayloadJson)!;
        Assert.Equal(actorId, entry.ActorUserId);
        Assert.Equal(_clock.UtcNow, entry.OccurredAtUtc);
        Assert.Equal(report.Id.ToString(), entry.TargetId);
        using var metadata = JsonDocument.Parse(entry.MetadataJson!);
        Assert.Equal(2, metadata.RootElement.GetProperty("CurrentVersionNo").GetInt32());
        Assert.Equal(lockedFile.Id, metadata.RootElement.GetProperty("FileId").GetGuid());
        Assert.Equal(lockedFile.ChecksumSha256, metadata.RootElement.GetProperty("ChecksumSha256").GetString());
        Assert.Equal(2, _storage.Uploads.Count);
        Assert.Empty(_storage.Deletes);
        Assert.Empty(_storage.Downloads);

        using var blockedRevision = await SubmitVersionAsync(3);
        Assert.Equal(HttpStatusCode.Conflict, blockedRevision.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.EvaluationStarted.Code, await blockedRevision.Content.ReadAsStringAsync());
        Assert.Equal(2, _storage.Uploads.Count);
        using var history = await _client.GetAsync(HistoryRoute);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Contains("UnderEvaluation", await history.Content.ReadAsStringAsync());
        using var download = await _client.GetAsync(DownloadRoute(lockedFile.Id));
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("OtherLecturer")]
    [InlineData("FacultyWithPermission")]
    public async Task Read_access_does_not_grant_permission_to_begin_evaluation(string reader)
    {
        var (report, _, _) = await PrepareEvaluationAsync();
        if (reader == "OtherLecturer") Authenticate(Guid.NewGuid(), "Lecturer");
        if (reader == "FacultyWithPermission")
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                $"{Guid.NewGuid():N}.Faculty.{PermissionCodes.ThesisReviewGatesReview}");
        }

        using var response = await StartEvaluationAsync(report.Id, 1);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.EvaluationAccessDenied.Code, await response.Content.ReadAsStringAsync());
        await AssertStoredVersionsAsync(1);
        Assert.Empty(_storage.Deletes);
        Assert.Empty(_storage.Downloads);
    }

    [Fact]
    public async Task Only_the_currently_assigned_advisor_can_begin_evaluation()
    {
        var (report, _, oldAdvisor) = await PrepareEvaluationAsync();
        await using var db = CreateThesisDbContext();
        var topicId = (await db.StudentGroups.SingleAsync()).TopicId;
        var newAdvisor = Guid.NewGuid();
        _advisorAssignments.Assignments[topicId] = newAdvisor;

        using var oldAttempt = await StartEvaluationAsync(report.Id, 1, oldAdvisor);
        using var newAttempt = await StartEvaluationAsync(report.Id, 1, newAdvisor);

        Assert.Equal(HttpStatusCode.Forbidden, oldAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newAttempt.StatusCode);
        Assert.Equal(1, await db.AuditOutboxMessages.CountAsync(row => row.Action == IntegrationAuditActions.FinalReportEvaluationStarted));
    }

    [Fact]
    public async Task Evaluation_can_begin_after_the_submission_window_has_closed()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        _clock.UtcNow = _batchReader.DeadlineAtUtc.AddDays(1);
        _batchReader.IsOpen = false;

        using var response = await StartEvaluationAsync(report.Id, 1, advisorId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Repeated_evaluation_start_does_not_create_a_second_audit_or_change_the_locked_version()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        using var first = await StartEvaluationAsync(report.Id, 1, advisorId);
        using var repeated = await StartEvaluationAsync(report.Id, 1, advisorId);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.InvalidEvaluationState.Code, await repeated.Content.ReadAsStringAsync());
        await using var db = CreateThesisDbContext();
        Assert.Equal(2, await db.AuditOutboxMessages.CountAsync());
        Assert.Equal(1, (await db.FinalReports.SingleAsync()).CurrentVersionNo);
        Assert.Single(await db.SubmissionFiles.ToListAsync());
        Assert.Single(_storage.Objects);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Evaluation_cannot_lock_a_different_version_than_the_one_the_advisor_reviewed(int expectedVersion)
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        using var revision = await SubmitVersionAsync(2);
        Assert.Equal(HttpStatusCode.OK, revision.StatusCode);

        using var response = await StartEvaluationAsync(report.Id, expectedVersion, advisorId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.ConcurrencyConflict.Code, await response.Content.ReadAsStringAsync());
        await AssertStoredVersionsAsync(2);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"expectedVersionNo\":0}")]
    [InlineData("{\"expectedVersionNo\":-1}")]
    [InlineData("{\"expectedVersionNo\":1.5}")]
    [InlineData("{broken")]
    [InlineData("")]
    public async Task Invalid_evaluation_bodies_return_400_without_mutation(string body)
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        Authenticate(advisorId, "Lecturer");
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync(EvaluationRoute(report.Id), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.InvalidRequest.Code, await response.Content.ReadAsStringAsync());
        await AssertStoredVersionsAsync(1);
    }

    [Fact]
    public async Task Evaluation_validates_identifiers_and_content_type()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        using var emptyId = await StartEvaluationAsync(Guid.Empty, 1, advisorId);
        using var missingId = await StartEvaluationAsync(Guid.NewGuid(), 1, advisorId);
        Assert.Equal(HttpStatusCode.BadRequest, emptyId.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingId.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.ReportNotFound.Code, await missingId.Content.ReadAsStringAsync());
        Authenticate(advisorId, "Lecturer");
        using var content = new StringContent("expectedVersionNo=1", Encoding.UTF8, "text/plain");
        using var wrongType = await _client.PostAsync(EvaluationRoute(report.Id), content);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongType.StatusCode);
        await AssertStoredVersionsAsync(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Evaluation_requires_an_authenticated_nonempty_user(bool emptyUserId)
    {
        var (report, _, _) = await PrepareEvaluationAsync();
        if (emptyUserId) Authenticate(Guid.Empty, "Admin"); else _client.DefaultRequestHeaders.Authorization = null;

        using var response = await StartEvaluationAsync(report.Id, 1);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertStoredVersionsAsync(1);
    }

    [Theory]
    [InlineData("Stopped", HttpStatusCode.Conflict, "FinalReport.InvalidGroupState")]
    [InlineData("MissingFile", HttpStatusCode.NotFound, "FinalReport.FileNotFound")]
    [InlineData("RevisedAfterReviewer", HttpStatusCode.Conflict, "FinalReport.InvalidEvaluationState")]
    public async Task Evaluation_requires_an_active_submission_with_its_current_file(string setup, HttpStatusCode expected, string code)
    {
        var (report, file, advisorId) = await PrepareEvaluationAsync();
        await using var db = CreateThesisDbContext();
        var targetId = report.Id;
        if (setup == "Stopped")
        {
            var group = await db.StudentGroups.SingleAsync();
            Assert.True(group.TryStop());
        }
        else if (setup == "MissingFile") db.SubmissionFiles.Remove(file);
        else
        {
            var revised = FinalReport.CreateSubmitted(Guid.NewGuid(), _groupId, FinalReportVersionType.RevisedAfterReviewer, Now);
            db.FinalReports.Add(revised);
            db.SubmissionFiles.Add(CopyFile(file, SubmissionEntityType.FinalReport, revised.Id));
            targetId = revised.Id;
        }
        await db.SaveChangesAsync();

        using var response = await StartEvaluationAsync(targetId, 1, advisorId);

        Assert.Equal(expected, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync());
        Assert.All(await db.FinalReports.AsNoTracking().ToListAsync(), value => Assert.Equal(FinalReportStatus.Submitted, value.Status));
        Assert.Equal(1, await db.AuditOutboxMessages.CountAsync());
        Assert.Empty(_storage.Deletes);
        Assert.Empty(_storage.Downloads);
    }

    [Fact]
    public async Task Concurrent_evaluation_starts_only_commit_one_transition_and_audit()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        var bothReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        _beforeSave = async (_, token) =>
        {
            if (Interlocked.Increment(ref arrivals) == 2) bothReady.TrySetResult(true);
            await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        };

        var responses = await Task.WhenAll(StartEvaluationAsync(report.Id, 1, advisorId),
            StartEvaluationAsync(report.Id, 1, Guid.NewGuid(), "Admin"));
        using var first = responses[0];
        using var second = responses[1];

        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(response => response.StatusCode).Order());
        Assert.Contains(FinalReportSubmissionErrors.ConcurrencyConflict.Code,
            await responses.Single(response => response.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync());
        await using var db = CreateThesisDbContext();
        Assert.Equal(FinalReportStatus.UnderEvaluation, (await db.FinalReports.SingleAsync()).Status);
        Assert.Equal(2, await db.AuditOutboxMessages.CountAsync());
        Assert.Single(await db.SubmissionFiles.ToListAsync());
        Assert.Single(_storage.Objects);
    }

    [Fact]
    public async Task Evaluation_committed_during_revision_upload_keeps_the_previous_file_locked()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        _storage.AfterUpload = async _ =>
        {
            using var started = await StartEvaluationAsync(report.Id, 1, advisorId);
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        };

        using var revision = await SubmitVersionAsync(2);

        Assert.Equal(HttpStatusCode.Conflict, revision.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.ConcurrencyConflict.Code, await revision.Content.ReadAsStringAsync());
        await using var db = CreateThesisDbContext();
        var locked = await db.FinalReports.SingleAsync();
        Assert.Equal(FinalReportStatus.UnderEvaluation, locked.Status);
        Assert.Equal(1, locked.CurrentVersionNo);
        Assert.Single(await db.SubmissionFiles.ToListAsync());
        Assert.Equal(2, await db.AuditOutboxMessages.CountAsync());
        Assert.Single(_storage.Objects);
        Assert.Single(_storage.Deletes);
    }

    [Fact]
    public async Task A_revision_committed_before_evaluation_save_rejects_the_stale_start()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        _beforeSave = async (db, _) =>
        {
            if (db.ChangeTracker.Entries<FinalReport>().Any(entry => entry.Entity.Status == FinalReportStatus.UnderEvaluation))
            {
                using var revision = await SubmitVersionAsync(2);
                Assert.Equal(HttpStatusCode.OK, revision.StatusCode);
            }
        };

        using var response = await StartEvaluationAsync(report.Id, 1, advisorId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.ConcurrencyConflict.Code, await response.Content.ReadAsStringAsync());
        await AssertStoredVersionsAsync(2);
        Assert.Empty(_storage.Deletes);
    }

    [Fact]
    public async Task A_group_stopped_before_evaluation_save_prevents_the_transition_and_audit()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        _beforeSave = async (_, token) =>
        {
            await using var db = CreateThesisDbContext();
            var group = await db.StudentGroups.SingleAsync(token);
            Assert.True(group.TryStop());
            await db.SaveChangesAsync(token);
        };

        using var response = await StartEvaluationAsync(report.Id, 1, advisorId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.ConcurrencyConflict.Code, await response.Content.ReadAsStringAsync());
        await AssertStoredVersionsAsync(1, groupStatus: StudentGroupStatus.Stopped);
    }

    [Fact]
    public async Task Audit_failure_rolls_back_evaluation_and_allows_a_later_retry()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        await using var db = CreateThesisDbContext();
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE thesis.audit_outbox_message ADD CONSTRAINT force_evaluation_audit_failure
            CHECK (action <> 'ThesisManagement.FinalReport.EvaluationStarted')
            """);

        using var response = await StartEvaluationAsync(report.Id, 1, advisorId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertStoredVersionsAsync(1);
        Assert.Empty(_storage.Deletes);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE thesis.audit_outbox_message DROP CONSTRAINT force_evaluation_audit_failure");
        using var retry = await StartEvaluationAsync(report.Id, 1, advisorId);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task Losing_commit_acknowledgement_does_not_unlock_the_report_or_duplicate_the_audit()
    {
        var (report, _, advisorId) = await PrepareEvaluationAsync();
        _failureAfterCommit = new IOException("Simulated lost evaluation commit acknowledgement");

        using var response = await StartEvaluationAsync(report.Id, 1, advisorId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        _failureAfterCommit = null;
        using var retry = await StartEvaluationAsync(report.Id, 1, advisorId);
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        await using var db = CreateThesisDbContext();
        Assert.Equal(FinalReportStatus.UnderEvaluation, (await db.FinalReports.SingleAsync()).Status);
        Assert.Equal(2, await db.AuditOutboxMessages.CountAsync());
        Assert.Single(_storage.Objects);
        Assert.Empty(_storage.Deletes);
    }

    private async Task<(FinalReport Report, SubmissionFile File, Guid AdvisorId)> PrepareEvaluationAsync()
    {
        using var submission = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
        await using var db = CreateThesisDbContext();
        var group = await db.StudentGroups.SingleAsync();
        var advisorId = Guid.NewGuid();
        _advisorAssignments.Assignments[group.TopicId] = advisorId;
        return (await db.FinalReports.SingleAsync(), await db.SubmissionFiles.SingleAsync(), advisorId);
    }

    private async Task<HttpResponseMessage> StartEvaluationAsync(Guid reportId, int expectedVersionNo,
        Guid? actorId = null, string role = "Lecturer")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EvaluationRoute(reportId))
        {
            Content = JsonContent.Create(new { expectedVersionNo })
        };
        if (actorId.HasValue) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{actorId:N}.{role}");
        return await _client.SendAsync(request);
    }
}
