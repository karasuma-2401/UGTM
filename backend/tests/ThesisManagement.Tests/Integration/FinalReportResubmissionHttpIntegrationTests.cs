using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportSubmissionHttpIntegrationTests
{
    [Fact]
    public async Task Resubmissions_keep_the_report_identity_and_preserve_every_file_and_audit()
    {
        await ConfirmPartnerAsync();
        Guid? reportId = null;
        for (var version = 1; version <= 3; version++)
        {
            _clock.UtcNow = Now.AddMinutes(version - 1);
            var studentId = version == 2 ? _partnerId : _studentId;
            using var response = await SubmitVersionAsync(version, studentId);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var data = json.RootElement.GetProperty("data");
            reportId ??= data.GetProperty("finalReportId").GetGuid();
            Assert.Equal(reportId, data.GetProperty("finalReportId").GetGuid());
            Assert.Equal(version, data.GetProperty("versionNo").GetInt32());
            Assert.Equal("InitialFinal", data.GetProperty("versionType").GetString());
            Assert.Equal("Submitted", data.GetProperty("status").GetString());
            Assert.Equal(_clock.UtcNow, data.GetProperty("submittedAtUtc").GetDateTime());
            Assert.Equal(studentId, data.GetProperty("file").GetProperty("uploadedBy").GetGuid());
        }

        await using var db = CreateThesisDbContext();
        var report = await db.FinalReports.SingleAsync();
        Assert.Equal(reportId, report.Id);
        Assert.Equal(3, report.CurrentVersionNo);
        Assert.Equal(Now.AddMinutes(2), report.SubmittedAt);
        Assert.Equal(StudentGroupStatus.FinalReportSubmitted, (await db.StudentGroups.SingleAsync()).Status);
        var files = await db.SubmissionFiles.OrderBy(file => file.VersionNo).ToListAsync();
        Assert.Equal(new[] { 1, 2, 3 }, files.Select(file => file.VersionNo));
        Assert.Equal(3, files.Select(file => file.ObjectKey).Distinct().Count());
        Assert.All(files, file =>
        {
            Assert.Equal(report.Id, file.EntityId);
            Assert.Equal(SubmissionEntityType.FinalReport, file.EntityType);
            Assert.Equal(Now.AddMinutes(file.VersionNo - 1), file.UploadedAt);
            Assert.Equal(VersionContent(file.VersionNo), _storage.Objects[file.ObjectKey]);
        });
        var auditVersions = new List<int>();
        foreach (var audit in await db.AuditOutboxMessages.ToListAsync())
        {
            var entry = JsonSerializer.Deserialize<UGTM.Modules.Identity.Contracts.Auditing.AuditIntegrationEvent>(audit.PayloadJson)!;
            Assert.Equal(report.Id.ToString(), entry.TargetId);
            using var metadata = JsonDocument.Parse(entry.MetadataJson!);
            var version = metadata.RootElement.GetProperty("CurrentVersionNo").GetInt32();
            auditVersions.Add(version);
            Assert.Equal(version != 1, metadata.RootElement.GetProperty("IsResubmission").GetBoolean());
            Assert.Equal(files[version - 1].Id, metadata.RootElement.GetProperty("FileId").GetGuid());
        }
        Assert.Equal(new[] { 1, 2, 3 }, auditVersions.Order());
        Assert.Equal(3, _storage.Objects.Count);
        Assert.Empty(_storage.Deletes);
    }

    [Fact]
    public async Task Invalid_revision_does_not_advance_the_sequence_or_remove_the_previous_file()
    {
        using var first = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using (var form = Form("not a PDF"u8.ToArray()))
        using (var invalid = await _client.PostAsync("/api/thesis/final-reports", form))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        await AssertStoredVersionsAsync(1);
        using var revision = await SubmitVersionAsync(2);
        Assert.Equal(HttpStatusCode.OK, revision.StatusCode);
        await AssertStoredVersionsAsync(2);
        Assert.Empty(_storage.Deletes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resubmission_uses_the_same_inclusive_deadline_as_the_first_submission(bool afterDeadline)
    {
        using var first = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        _clock.UtcNow = _batchReader.DeadlineAtUtc.AddTicks(afterDeadline ? 1 : 0);

        using var revision = await SubmitVersionAsync(2);

        Assert.Equal(afterDeadline ? HttpStatusCode.Conflict : HttpStatusCode.OK, revision.StatusCode);
        if (afterDeadline)
        {
            Assert.Contains(FinalReportSubmissionErrors.OutsideAllowedWindow.Code, await revision.Content.ReadAsStringAsync());
        }
        await AssertStoredVersionsAsync(afterDeadline ? 1 : 2);
        Assert.Equal(afterDeadline ? 1 : 2, _storage.Uploads.Count);
        Assert.Empty(_storage.Deletes);
    }

    [Fact]
    public async Task Evaluation_already_started_rejects_a_revision_before_upload()
    {
        using var first = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await BeginEvaluationAsync();

        using var revision = await SubmitVersionAsync(2);

        Assert.Equal(HttpStatusCode.Conflict, revision.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.EvaluationStarted.Code, await revision.Content.ReadAsStringAsync());
        await AssertStoredVersionsAsync(1, FinalReportStatus.UnderEvaluation);
        Assert.Single(_storage.Uploads);
        Assert.Empty(_storage.Deletes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_members_keep_only_the_winning_submission(bool isResubmission)
    {
        await ConfirmPartnerAsync();
        if (isResubmission)
        {
            using var first = await SubmitVersionAsync(1);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }
        var bothUploaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var uploadCount = 0;
        _storage.AfterUpload = async token =>
        {
            if (Interlocked.Increment(ref uploadCount) == 2) bothUploaded.TrySetResult(true);
            await bothUploaded.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        };

        var responses = await Task.WhenAll(SubmitVersionAsync(100, _studentId), SubmitVersionAsync(200, _partnerId));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(response => response.StatusCode).Order());
        var conflict = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Contains(FinalReportSubmissionErrors.ConcurrencyConflict.Code, await conflict.Content.ReadAsStringAsync());
        var expectedVersion = isResubmission ? 2 : 1;
        await using var db = CreateThesisDbContext();
        var report = await db.FinalReports.SingleAsync();
        Assert.Equal(expectedVersion, report.CurrentVersionNo);
        var files = await db.SubmissionFiles.OrderBy(file => file.VersionNo).ToListAsync();
        Assert.Equal(Enumerable.Range(1, expectedVersion), files.Select(file => file.VersionNo));
        Assert.Equal(expectedVersion, await db.AuditOutboxMessages.CountAsync());
        Assert.Equal(expectedVersion, _storage.Objects.Count);
        Assert.All(files, file => Assert.True(_storage.Objects.ContainsKey(file.ObjectKey)));
        if (isResubmission) Assert.Equal(VersionContent(1), _storage.Objects[files[0].ObjectKey]);
        Assert.Equal(isResubmission ? 3 : 2, _storage.Uploads.Count);
        var deletedObject = Assert.Single(_storage.Deletes);
        Assert.DoesNotContain(files, file => file.ObjectKey == deletedObject);
    }

    [Fact]
    public async Task Evaluation_started_during_upload_wins_and_the_stale_revision_is_rolled_back()
    {
        using var first = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        _storage.AfterUpload = _ => BeginEvaluationAsync();

        using var revision = await SubmitVersionAsync(2);

        Assert.Equal(HttpStatusCode.Conflict, revision.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.ConcurrencyConflict.Code, await revision.Content.ReadAsStringAsync());
        await AssertStoredVersionsAsync(1, FinalReportStatus.UnderEvaluation);
        Assert.Single(_storage.Deletes);
    }

    [Fact]
    public async Task A_revision_committed_first_prevents_evaluation_from_saving_a_stale_version()
    {
        using var first = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await using var staleEvaluation = CreateThesisDbContext();
        var staleReport = await staleEvaluation.FinalReports.SingleAsync();
        Assert.True(staleReport.TryBeginEvaluation());

        using var revision = await SubmitVersionAsync(2);

        Assert.Equal(HttpStatusCode.OK, revision.StatusCode);
        await Assert.ThrowsAsync<ThesisManagementConcurrencyException>(() => staleEvaluation.SaveChangesAsync());
        await AssertStoredVersionsAsync(2);
        Assert.Empty(_storage.Deletes);
    }

    [Fact]
    public async Task A_group_stopped_during_revision_upload_cannot_receive_the_revision()
    {
        using var first = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        _storage.AfterUpload = async _ =>
        {
            await using var db = CreateThesisDbContext();
            var group = await db.StudentGroups.SingleAsync();
            Assert.True(group.TryStop());
            await db.SaveChangesAsync();
        };

        using var revision = await SubmitVersionAsync(2);

        Assert.Equal(HttpStatusCode.Conflict, revision.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.ConcurrencyConflict.Code, await revision.Content.ReadAsStringAsync());
        await AssertStoredVersionsAsync(1, groupStatus: StudentGroupStatus.Stopped);
        Assert.Single(_storage.Deletes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_revision_preserves_the_previous_version_and_only_deletes_the_new_object(bool databaseFailure)
    {
        using var first = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        if (databaseFailure)
        {
            await using var db = CreateThesisDbContext();
            // Existing audit rows remain valid; only the new insert is forced to fail.
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE thesis.audit_outbox_message ADD CONSTRAINT force_revision_audit_failure
                CHECK (action <> 'ThesisManagement.FinalReport.Submitted') NOT VALID
                """);
        }
        else
        {
            _storage.UploadFailure = new IOException("Revision upload failed after accepting an object.");
        }

        using var revision = await SubmitVersionAsync(2);

        Assert.Equal(HttpStatusCode.InternalServerError, revision.StatusCode);
        await AssertStoredVersionsAsync(1);
        Assert.Single(_storage.Deletes);
        Assert.Equal(2, _storage.Uploads.Count);
    }

    private async Task<HttpResponseMessage> SubmitVersionAsync(int contentVersion, Guid? studentId = null)
    {
        using var form = Form(VersionContent(contentVersion), $"report-v{contentVersion}.pdf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/thesis/final-reports") { Content = form };
        if (studentId.HasValue) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{studentId:N}.Student");
        return await _client.SendAsync(request);
    }

    private async Task ConfirmPartnerAsync()
    {
        await using var db = CreateThesisDbContext();
        var group = await db.StudentGroups.Include(value => value.Members).SingleAsync();
        Assert.True(group.ConfirmMember(_partnerId, Now));
        await db.SaveChangesAsync();
    }

    private async Task BeginEvaluationAsync()
    {
        await using var db = CreateThesisDbContext();
        var report = await db.FinalReports.SingleAsync();
        Assert.True(report.TryBeginEvaluation());
        await db.SaveChangesAsync();
    }

    private async Task AssertStoredVersionsAsync(int count, FinalReportStatus status = FinalReportStatus.Submitted,
        StudentGroupStatus groupStatus = StudentGroupStatus.FinalReportSubmitted)
    {
        await using var db = CreateThesisDbContext();
        var report = await db.FinalReports.SingleAsync();
        Assert.Equal(count, report.CurrentVersionNo);
        Assert.Equal(status, report.Status);
        Assert.Equal(FinalReportVersionType.InitialFinal, report.VersionType);
        Assert.Equal(groupStatus, (await db.StudentGroups.SingleAsync()).Status);
        var files = await db.SubmissionFiles.OrderBy(file => file.VersionNo).ToListAsync();
        Assert.Equal(Enumerable.Range(1, count), files.Select(file => file.VersionNo));
        Assert.All(files, file =>
        {
            Assert.Equal(report.Id, file.EntityId);
            Assert.Equal(VersionContent(file.VersionNo), _storage.Objects[file.ObjectKey]);
        });
        Assert.Equal(count, await db.AuditOutboxMessages.CountAsync());
        Assert.Equal(count, _storage.Objects.Count);
    }

    private static byte[] VersionContent(int version) => Encoding.ASCII.GetBytes($"%PDF-1.7\nFinal report version {version}");
}
