using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class FinalReportSubmissionHttpIntegrationTests
{
    private string HistoryRoute => $"/api/thesis/groups/{_groupId}/final-report";
    private static string DownloadRoute(Guid fileId) => $"/api/thesis/final-report-files/{fileId}/download";

    [Fact]
    public async Task History_returns_all_versions_in_order_and_each_version_can_be_downloaded()
    {
        await ConfirmPartnerAsync();
        for (var version = 1; version <= 3; version++)
        {
            _clock.UtcNow = Now.AddMinutes(version - 1);
            using var submission = await SubmitVersionAsync(version, version == 2 ? _partnerId : _studentId);
            Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
        }
        await using var db = CreateThesisDbContext();
        var report = await db.FinalReports.SingleAsync();
        var files = await db.SubmissionFiles.OrderBy(file => file.VersionNo).ToListAsync();

        using var response = await _client.GetAsync(HistoryRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(report.Id, data.GetProperty("finalReportId").GetGuid());
        Assert.Equal(_groupId, data.GetProperty("groupId").GetGuid());
        Assert.Equal("InitialFinal", data.GetProperty("versionType").GetString());
        Assert.Equal(3, data.GetProperty("currentVersionNo").GetInt32());
        Assert.Equal("Submitted", data.GetProperty("currentStatus").GetString());
        Assert.Equal(Now.AddMinutes(2), data.GetProperty("submittedAtUtc").GetDateTime());
        var versions = data.GetProperty("versions").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, versions.Select(value => value.GetProperty("versionNo").GetInt32()));
        Assert.Empty(_storage.Downloads);
        for (var index = 0; index < versions.Length; index++)
        {
            var file = files[index];
            var metadata = versions[index].GetProperty("file");
            Assert.Equal(file.Id, metadata.GetProperty("id").GetGuid());
            Assert.Equal(file.FileName, metadata.GetProperty("fileName").GetString());
            Assert.Equal(file.ChecksumSha256, metadata.GetProperty("checksumSha256").GetString());
            Assert.Equal(file.UploadedBy, metadata.GetProperty("uploadedBy").GetGuid());
            Assert.Equal(file.UploadedAt, metadata.GetProperty("uploadedAtUtc").GetDateTime());
            Assert.False(metadata.TryGetProperty("objectKey", out _));
            Assert.False(metadata.TryGetProperty("downloadUrl", out _));
            using var download = await _client.GetAsync(DownloadRoute(file.Id));
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            using var downloadJson = JsonDocument.Parse(await download.Content.ReadAsStringAsync());
            var downloadData = downloadJson.RootElement.GetProperty("data");
            Assert.Equal(file.Id, downloadData.GetProperty("fileId").GetGuid());
            Assert.Equal(_clock.UtcNow.AddMinutes(5), downloadData.GetProperty("expiresAtUtc").GetDateTime());
            Assert.StartsWith("https://storage.test/download/", downloadData.GetProperty("downloadUrl").GetString());
            Assert.False(downloadData.TryGetProperty("objectKey", out _));
        }
        Assert.Equal(files.Select(file => file.ObjectKey), _storage.Downloads.Select(download => download.ObjectKey));
        Assert.All(_storage.Downloads, download => Assert.Equal(TimeSpan.FromMinutes(5), download.ValidFor));
        Assert.Equal(3, await db.AuditOutboxMessages.CountAsync());
        Assert.Empty(_storage.Deletes);
    }

    [Theory]
    [InlineData("Student", true)]
    [InlineData("ConfirmedPartner", true)]
    [InlineData("Advisor", true)]
    [InlineData("FacultyWithPermission", true)]
    [InlineData("Admin", true)]
    [InlineData("PendingPartner", false)]
    [InlineData("OtherStudent", false)]
    [InlineData("FacultyWithoutPermission", false)]
    [InlineData("PermissionWithoutFaculty", false)]
    [InlineData("OldAdvisor", false)]
    [InlineData("MemberWithoutStudentRole", false)]
    public async Task History_and_download_enforce_the_same_group_access(string reader, bool allowed)
    {
        using var submission = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
        await using var db = CreateThesisDbContext();
        var group = await db.StudentGroups.SingleAsync();
        var file = await db.SubmissionFiles.SingleAsync();
        var advisorId = Guid.NewGuid();
        _advisorAssignments.Assignments[group.TopicId] = advisorId;
        if (reader == "ConfirmedPartner") await ConfirmPartnerAsync();
        var (userId, role, permission) = reader switch
        {
            "Student" => (_studentId, "sTuDeNt", (string?)null),
            "ConfirmedPartner" or "PendingPartner" => (_partnerId, "Student", null),
            "Advisor" => (advisorId, "Lecturer", null),
            "FacultyWithPermission" => (Guid.NewGuid(), "fAcUlTy", PermissionCodes.ThesisReviewGatesReview),
            "Admin" => (Guid.NewGuid(), "aDmIn", null),
            "FacultyWithoutPermission" => (Guid.NewGuid(), "Faculty", null),
            "PermissionWithoutFaculty" => (Guid.NewGuid(), "Student", PermissionCodes.ThesisReviewGatesReview),
            "OldAdvisor" => (Guid.NewGuid(), "Lecturer", null),
            "MemberWithoutStudentRole" => (_studentId, "Lecturer", null),
            _ => (Guid.NewGuid(), "Student", null)
        };
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            $"{userId:N}.{role}" + (permission is null ? "" : $".{permission}"));

        using var history = await _client.GetAsync(HistoryRoute);
        using var download = await _client.GetAsync(DownloadRoute(file.Id));

        var expected = allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
        Assert.Equal(expected, history.StatusCode);
        Assert.Equal(expected, download.StatusCode);
        Assert.Equal(allowed ? 1 : 0, _storage.Downloads.Count);
        if (!allowed)
        {
            Assert.Contains(FinalReportSubmissionErrors.ReadAccessDenied.Code, await history.Content.ReadAsStringAsync());
            Assert.Contains(FinalReportSubmissionErrors.ReadAccessDenied.Code, await download.Content.ReadAsStringAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reads_remain_available_after_deadline_when_evaluation_started_or_group_stopped(bool stopped)
    {
        using var submission = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
        await BeginEvaluationAsync();
        await using var db = CreateThesisDbContext();
        if (stopped)
        {
            var group = await db.StudentGroups.SingleAsync();
            Assert.True(group.TryStop());
            await db.SaveChangesAsync();
        }
        _clock.UtcNow = _batchReader.DeadlineAtUtc.AddDays(1);
        _batchReader.IsOpen = false;
        var file = await db.SubmissionFiles.SingleAsync();

        using var history = await _client.GetAsync(HistoryRoute);
        using var download = await _client.GetAsync(DownloadRoute(file.Id));

        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Contains("UnderEvaluation", await history.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_endpoints_require_an_authenticated_nonempty_user(bool emptyUserId)
    {
        using var submission = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
        await using var db = CreateThesisDbContext();
        var file = await db.SubmissionFiles.SingleAsync();
        if (emptyUserId) Authenticate(Guid.Empty); else _client.DefaultRequestHeaders.Authorization = null;

        using var history = await _client.GetAsync(HistoryRoute);
        using var download = await _client.GetAsync(DownloadRoute(file.Id));

        Assert.Equal(HttpStatusCode.Unauthorized, history.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, download.StatusCode);
        Assert.Empty(_storage.Downloads);
    }

    [Fact]
    public async Task Missing_groups_reports_files_and_empty_identifiers_return_expected_errors()
    {
        using var missingGroup = await _client.GetAsync($"/api/thesis/groups/{Guid.NewGuid()}/final-report");
        Assert.Equal(HttpStatusCode.NotFound, missingGroup.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.GroupNotFound.Code, await missingGroup.Content.ReadAsStringAsync());
        using var missingReport = await _client.GetAsync(HistoryRoute);
        Assert.Equal(HttpStatusCode.NotFound, missingReport.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.ReportNotFound.Code, await missingReport.Content.ReadAsStringAsync());
        using var missingFile = await _client.GetAsync(DownloadRoute(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, missingFile.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.FileNotFound.Code, await missingFile.Content.ReadAsStringAsync());
        using var emptyGroup = await _client.GetAsync($"/api/thesis/groups/{Guid.Empty}/final-report");
        using var emptyFile = await _client.GetAsync(DownloadRoute(Guid.Empty));
        Assert.Equal(HttpStatusCode.BadRequest, emptyGroup.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyFile.StatusCode);
        // An outsider must not learn whether the group's report exists.
        Authenticate(Guid.NewGuid());
        using var forbidden = await _client.GetAsync(HistoryRoute);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Empty(_storage.Downloads);
    }

    [Theory]
    [InlineData(SubmissionEntityType.Syllabus)]
    [InlineData(SubmissionEntityType.MidtermReport)]
    [InlineData(SubmissionEntityType.ProgressReport)]
    public async Task Download_rejects_files_of_another_entity_type_even_with_the_same_entity_id(SubmissionEntityType entityType)
    {
        using var submission = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
        await using var db = CreateThesisDbContext();
        var existing = await db.SubmissionFiles.SingleAsync();
        var unrelated = CopyFile(existing, entityType, existing.EntityId);
        db.SubmissionFiles.Add(unrelated);
        await db.SaveChangesAsync();

        using var response = await _client.GetAsync(DownloadRoute(unrelated.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_storage.Downloads);
        using var history = await _client.GetAsync(HistoryRoute);
        using var json = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Single(json.RootElement.GetProperty("data").GetProperty("versions").EnumerateArray());
    }

    [Fact]
    public async Task Advisor_reassignment_revokes_old_access_and_grants_access_to_the_new_advisor()
    {
        using var submission = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
        await using var db = CreateThesisDbContext();
        var group = await db.StudentGroups.SingleAsync();
        var file = await db.SubmissionFiles.SingleAsync();
        var oldAdvisor = Guid.NewGuid();
        var newAdvisor = Guid.NewGuid();
        _advisorAssignments.Assignments[group.TopicId] = oldAdvisor;
        Authenticate(oldAdvisor, "Lecturer");
        using var initialHistory = await _client.GetAsync(HistoryRoute);
        using var initialDownload = await _client.GetAsync(DownloadRoute(file.Id));
        Assert.Equal(HttpStatusCode.OK, initialHistory.StatusCode);
        Assert.Equal(HttpStatusCode.OK, initialDownload.StatusCode);

        _advisorAssignments.Assignments[group.TopicId] = newAdvisor;
        using var oldHistory = await _client.GetAsync(HistoryRoute);
        using var oldDownload = await _client.GetAsync(DownloadRoute(file.Id));
        Assert.Equal(HttpStatusCode.Forbidden, oldHistory.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, oldDownload.StatusCode);
        Assert.Single(_storage.Downloads);

        Authenticate(newAdvisor, "Lecturer");
        using var newHistory = await _client.GetAsync(HistoryRoute);
        using var newDownload = await _client.GetAsync(DownloadRoute(file.Id));
        Assert.Equal(HttpStatusCode.OK, newHistory.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newDownload.StatusCode);
        Assert.Equal(2, _storage.Downloads.Count);
    }

    [Fact]
    public async Task Other_download_endpoints_cannot_be_used_for_final_report_files()
    {
        using var submission = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
        await using var db = CreateThesisDbContext();
        var file = await db.SubmissionFiles.SingleAsync();

        using var reviewGateDownload = await _client.GetAsync($"/api/thesis/submission-files/{file.Id}/download");
        using var progressDownload = await _client.GetAsync($"/api/thesis/progress-report-files/{file.Id}/download");

        Assert.Equal(HttpStatusCode.NotFound, reviewGateDownload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, progressDownload.StatusCode);
        Assert.Empty(_storage.Downloads);
    }

    [Fact]
    public async Task A_file_without_its_final_report_cannot_receive_a_download_url()
    {
        using var submission = await SubmitVersionAsync(1);
        Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
        await using var db = CreateThesisDbContext();
        var existing = await db.SubmissionFiles.SingleAsync();
        var orphan = CopyFile(existing, SubmissionEntityType.FinalReport, Guid.NewGuid());
        db.SubmissionFiles.Add(orphan);
        await db.SaveChangesAsync();

        using var response = await _client.GetAsync(DownloadRoute(orphan.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_storage.Downloads);
    }

    private static SubmissionFile CopyFile(SubmissionFile file, SubmissionEntityType entityType, Guid entityId) => new()
    {
        Id = Guid.NewGuid(),
        EntityType = entityType,
        EntityId = entityId,
        VersionNo = file.VersionNo,
        FileName = file.FileName,
        ObjectKey = $"test/{Guid.NewGuid():N}.pdf",
        ContentType = file.ContentType,
        ChecksumSha256 = file.ChecksumSha256,
        FileSize = file.FileSize,
        FileExtension = file.FileExtension,
        UploadedBy = file.UploadedBy,
        UploadedAt = file.UploadedAt
    };
}
