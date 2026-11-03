using System.Net;
using System.IO.Compression;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Application.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Storage;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

// Uses production host/owner readers/coordinator and actual PostgreSQL/MinIO. Locked is seeded:
// the producer API that locks a defense minute remains outside SCRUM-106/110.
public sealed partial class FinalReportWorkflowIntegrationTests
{
    private const string ArchivalRoute = "/api/thesis/final-submissions";
    private Func<string, Task>? _beforeArchivalUpload;
    private Func<string, Task>? _afterArchivalUpload;
    private bool _failArchivalDeletion;
    private readonly ConcurrentQueue<string> _archivalErrors = new();
    private void ConfigureArchivalStorage(IServiceCollection services)
        => services.AddSingleton<IThesisSubmissionObjectStorage>(sp => new ArchivalControlledStorage(
            new S3ThesisSubmissionObjectStorage(sp.GetRequiredService<IAmazonS3>(), sp.GetRequiredService<IOptions<S3StorageOptions>>()), this));

    private async Task PrepareArchivalAsync()
    {
        await using (var identity = Db<IdentityDbContext>())
        {
            var roles = await identity.Roles.ToDictionaryAsync(r => r.RoleName, r => r.Id);
            identity.UserRoles.AddRange(new UserRole { UserId = _studentId, RoleId = roles["Student"], AssignedAt = SeedTime },
                new UserRole { UserId = _partnerId, RoleId = roles["Student"], AssignedAt = SeedTime },
                new UserRole { UserId = _advisorId, RoleId = roles["Lecturer"], AssignedAt = SeedTime },
                new UserRole { UserId = _adminId, RoleId = roles["Admin"], AssignedAt = SeedTime });
            await identity.SaveChangesAsync();
        }
        await using (var academic = Db<AcademicPeriodsDbContext>())
        {
            var stage = await academic.Stages.SingleAsync(s => s.BatchId == _batchId && s.StageCode == "ARCHIVAL_AND_PUBLICATION");
            _clock.UtcNow = stage.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7).AddHours(1);
        }
        var council = Council.Create(_batchId, "ARCHIVAL", DateOnly.FromDateTime(_clock.UtcNow), "AM", "A101");
        council.Approve("ARCHIVAL-TEST", null, null, _adminId, _clock.UtcNow);
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = _groupId };
        await using var defense = Db<DefenseCouncilDbContext>();
        defense.Councils.Add(council);
        defense.CouncilAssignments.Add(assignment);
        defense.CouncilMembers.AddRange(new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = _adminId, RoleInCouncil = CouncilMemberRole.Chairman },
            new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = _replacementAdvisorId, RoleInCouncil = CouncilMemberRole.Secretary });
        defense.CouncilMinutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = assignment.Id,
            LockStatus = CouncilMinuteLockStatus.Locked,
            LockedBy = _adminId,
            LockedAt = _clock.UtcNow.AddDays(-1)
        });
        await defense.SaveChangesAsync();
    }
    private async Task<HttpResponseMessage> ArchiveAsync(Guid? key = null, uint? version = null, Action<MultipartFormDataContent>? mutate = null,
        Guid? student = null, byte[]? pdf = null, byte[]? docx = null, bool unicodeNames = false)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(_groupId.ToString()), "groupId");
        if (version.HasValue) form.Add(new StringContent(version.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "expectedSubmissionVersion");
        form.Add(new StringContent("  Tóm tắt  "), "abstractVi");
        form.Add(new StringContent("Abstract"), "abstractEn");
        form.Add(new StringContent("từ khóa"), "keywordsVi");
        form.Add(new StringContent("keywords"), "keywordsEn");
        form.Add(new ByteArrayContent(pdf ?? Document("pdf")), "pdfFile", unicodeNames ? "Báo cáo.pdf" : "archive.pdf");
        form.Add(new ByteArrayContent(docx ?? Document("docx")), "docxFile", unicodeNames ? "Báo cáo.docx" : "archive.docx");
        mutate?.Invoke(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, ArchivalRoute) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{student ?? _studentId:N}.Student");
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return await _client.SendAsync(request);
    }
    private async Task<JsonElement> ArchivalDataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body + Environment.NewLine + string.Join(Environment.NewLine, _archivalErrors));
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task Archival_real_package_replacement_history_private_download_receipt_and_actor_audit()
    {
        await PrepareArchivalAsync();
        var key = Guid.NewGuid();
        using var first = await ArchiveAsync(key, unicodeNames: true);
        var v1 = await ArchivalDataAsync(first);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var id = v1.GetProperty("submissionId").GetGuid();
        var version = v1.GetProperty("submissionVersion").GetUInt32();
        Assert.True(version > 0);
        Assert.Equal("Tóm tắt", v1.GetProperty("abstractVi").GetString());
        using var second = await ArchiveAsync(version: version, student: _partnerId);
        var v2 = await ArchivalDataAsync(second);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, v2.GetProperty("currentVersionNo").GetInt32());
        Assert.Equal(id, v2.GetProperty("submissionId").GetGuid());
        using var replay = await ArchiveAsync(key);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var history = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/{id}/packages", _studentId, "Student");
        var packages = (await ArchivalDataAsync(history)).EnumerateArray().ToArray();
        Assert.Equal(new[] { 2, 1 }, packages.Select(p => p.GetProperty("versionNo").GetInt32()));
        foreach (var role in new[] { "ThesisPdf", "ThesisDocx" })
        {
            using var link = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}/{id}/packages/1/files/{role}/download", _advisorId, "Lecturer");
            var dto = await ArchivalDataAsync(link);
            Assert.False(dto.TryGetProperty("objectKey", out _));
            Assert.Equal(_clock.UtcNow.AddSeconds(300), dto.GetProperty("expiresAt").GetDateTime());
            var url = dto.GetProperty("downloadUrl").GetString()!;
            // AWS signing serializes time at second precision and can ceil the requested TTL.
            Assert.InRange(int.Parse(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(url).Query)["X-Amz-Expires"].ToString(),
                System.Globalization.CultureInfo.InvariantCulture), 299, 301);
            using var anonymous = new HttpClient();
            using var downloaded = await anonymous.GetAsync(url);
            downloaded.EnsureSuccessStatusCode();
            Assert.Equal("attachment", downloaded.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal(role == "ThesisPdf" ? "Báo cáo.pdf" : "Báo cáo.docx", downloaded.Content.Headers.ContentDisposition?.FileNameStar);
            Assert.Equal(Document(role == "ThesisPdf" ? "pdf" : "docx"), await downloaded.Content.ReadAsByteArrayAsync());
        }
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Equal(2, await db.FinalSubmissionPackages.CountAsync());
        Assert.Equal(4, await db.SubmissionFiles.CountAsync());
        Assert.Equal(2, await db.FinalSubmissionIdempotencyRecords.CountAsync());
        Assert.All(await db.FinalSubmissionUploadAttempts.ToListAsync(), a => Assert.Equal("Committed", a.State));
        var audits = await db.AuditOutboxMessages.ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Equal(new[] { _studentId, _partnerId }.Order(), audits.Select(a => JsonSerializer.Deserialize<AuditIntegrationEvent>(a.PayloadJson)!.ActorUserId!.Value).Order());
        Assert.DoesNotContain("Tóm tắt", string.Join("", audits.Select(a => a.PayloadJson)));
        Assert.Equal(4, (await StoredKeysAsync()).Count);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("wrongFileRole")]
    [InlineData("metadata")]
    [InlineData("signature")]
    public async Task Archival_strict_multipart_validation_has_no_storage_side_effects(string kind)
    {
        await PrepareArchivalAsync();
        using var response = await ArchiveAsync(mutate: form =>
        {
            if (kind == "unknown") form.Add(new StringContent("unexpected"), "unknown");
            if (kind == "duplicate") form.Add(new StringContent(_groupId.ToString()), "GROUPID");
            if (kind == "wrongFileRole") form.Add(new ByteArrayContent(Document("pdf")), "pdfFile", "other.pdf");
            if (kind == "metadata") form.Add(new StringContent("duplicate"), "abstractVi");
        }, pdf: kind == "signature" ? "fake PDF"u8.ToArray() : null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await StoredKeysAsync());
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Empty(await db.FinalSubmissions.ToListAsync());
        Assert.Empty(await db.FinalSubmissionUploadAttempts.ToListAsync());
    }
    [Fact]
    public async Task Archival_two_large_valid_files_exceed_default_30MB_body_limit()
    {
        await PrepareArchivalAsync();
        var pdf = new byte[18 * 1024 * 1024];
        Document("pdf").CopyTo(pdf, 0);
        using var largeZip = new MemoryStream();
        largeZip.Write(Document("docx"));
        using (var zip = new ZipArchive(largeZip, ZipArchiveMode.Update, leaveOpen: true))
        using (var entry = zip.CreateEntry("word/media/padding.bin", CompressionLevel.NoCompression).Open())
            entry.Write(new byte[18 * 1024 * 1024]);
        var docx = largeZip.ToArray();
        using var response = await ArchiveAsync(pdf: pdf, docx: docx);
        var data = await ArchivalDataAsync(response);
        Assert.All(data.GetProperty("files").EnumerateArray(), f => Assert.InRange(f.GetProperty("fileSize").GetInt64(), 18 * 1024 * 1024, 20 * 1024 * 1024));
        Assert.Equal(2, (await StoredKeysAsync()).Count);
    }
    [Theory]
    [InlineData("batch")]
    [InlineData("membership")]
    [InlineData("minute")]
    [InlineData("account")]
    [InlineData("deadline")]
    public async Task Archival_changes_after_S3_are_revalidated_before_commit_and_attempt_is_cleaned(string change)
    {
        await PrepareArchivalAsync();
        _afterArchivalUpload = async key =>
        {
            if (!key.EndsWith(".docx", StringComparison.Ordinal)) return;
            await using var db = MigrationDb();
            switch (change)
            {
                case "batch": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic.kltn_batch SET status='Closed' WHERE id={_batchId}"); break;
                case "membership": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.student_group_member SET confirmation_status='Pending' WHERE group_id={_groupId} AND student_id={_studentId}"); break;
                case "minute": await db.Database.ExecuteSqlRawAsync("UPDATE councils.council_minute SET lock_status='AllScoresEntered'"); break;
                case "account": await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM identity.user_role WHERE user_id={_studentId}"); break;
                case "deadline": _clock.UtcNow = _clock.UtcNow.AddDays(30); break;
            }
        };
        using var response = await ArchiveAsync();
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.Forbidden });
        await using var verify = Db<ThesisManagementDbContext>();
        Assert.Empty(await verify.FinalSubmissions.ToListAsync());
        Assert.Empty(await verify.FinalSubmissionIdempotencyRecords.ToListAsync());
        Assert.Empty(await verify.AuditOutboxMessages.ToListAsync());
        Assert.Equal("Cleaned", (await verify.FinalSubmissionUploadAttempts.SingleAsync()).State);
        Assert.Empty(await StoredKeysAsync());
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Archival_concurrent_first_submissions_keep_one_package_and_no_orphan(bool sameKey)
    {
        await PrepareArchivalAsync();
        var arrivals = 0;
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _afterArchivalUpload = async key =>
        {
            if (!key.EndsWith(".docx", StringComparison.Ordinal)) return;
            if (Interlocked.Increment(ref arrivals) == 2) both.TrySetResult();
            await both.Task.WaitAsync(TimeSpan.FromSeconds(20));
        };
        var key = Guid.NewGuid();
        var results = await Task.WhenAll(ArchiveAsync(key), ArchiveAsync(sameKey ? key : Guid.NewGuid()));
        try
        {
            Assert.Equal(sameKey ? 2 : 1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
            Assert.Equal(sameKey ? 0 : 1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            await using var db = Db<ThesisManagementDbContext>();
            Assert.Single(await db.FinalSubmissionPackages.ToListAsync());
            Assert.Single(await db.FinalSubmissionIdempotencyRecords.ToListAsync());
            Assert.Single(await db.AuditOutboxMessages.ToListAsync());
            Assert.Equal(2, (await StoredKeysAsync()).Count);
            Assert.Equal(new[] { "Cleaned", "Committed" }, (await db.FinalSubmissionUploadAttempts.OrderBy(a => a.State).Select(a => a.State).ToArrayAsync()));
        }
        finally { foreach (var response in results) response.Dispose(); }
    }
    [Fact]
    public async Task Archival_failed_second_upload_and_failed_delete_are_durable_and_retriable()
    {
        await PrepareArchivalAsync();
        _beforeArchivalUpload = key => key.EndsWith(".docx", StringComparison.Ordinal) ? Task.FromException(new IOException("Injected upload failure")) : Task.CompletedTask;
        _failArchivalDeletion = true;
        using var response = await ArchiveAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var db = Db<ThesisManagementDbContext>();
        var attempt = await db.FinalSubmissionUploadAttempts.SingleAsync();
        Assert.Equal("CleanupPending", attempt.State);
        Assert.Single(await StoredKeysAsync());
        Assert.Empty(await db.FinalSubmissions.ToListAsync());
        _failArchivalDeletion = false;
        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IFinalSubmissionUploadCleanup>().CleanAsync(attempt.Id);
        await db.Entry(attempt).ReloadAsync();
        Assert.Equal("Cleaned", attempt.State);
        Assert.Empty(await StoredKeysAsync());
    }
    [Fact]
    public async Task Archival_receipt_failure_rolls_back_package_files_and_audit_then_cleans_S3()
    {
        await PrepareArchivalAsync();
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION thesis.fail_archival_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected receipt failure'; END $$;
            CREATE TRIGGER fail_archival_receipt BEFORE INSERT ON thesis.final_submission_idempotency FOR EACH ROW EXECUTE FUNCTION thesis.fail_archival_receipt();
            """);
        using var response = await ArchiveAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var verify = Db<ThesisManagementDbContext>();
        Assert.Empty(await verify.FinalSubmissions.ToListAsync());
        Assert.Empty(await verify.FinalSubmissionPackages.ToListAsync());
        Assert.Empty(await verify.SubmissionFiles.ToListAsync());
        Assert.Empty(await verify.AuditOutboxMessages.ToListAsync());
        Assert.Empty(await StoredKeysAsync());
    }
    [Theory]
    [InlineData("/my-group")]
    [InlineData("/submission-eligibility")]
    public async Task Archival_own_endpoints_require_an_explicit_confirmed_group_selector(string suffix)
    {
        await PrepareArchivalAsync();
        foreach (var query in new[] { "", "?groupId=bad", $"?groupId={_groupId}&groupId={_groupId}" })
        {
            using var response = await SendAsync(HttpMethod.Get, ArchivalRoute + suffix + query, _studentId, "Student");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var denied = await SendAsync(HttpMethod.Get, $"{ArchivalRoute}{suffix}?groupId={Guid.NewGuid()}", _studentId, "Student");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
    [Fact]
    public async Task Archival_replay_survives_batch_closure_but_loss_of_role_cannot_read_receipt()
    {
        await PrepareArchivalAsync();
        var key = Guid.NewGuid();
        using var first = await ArchiveAsync(key);
        var data = await ArchivalDataAsync(first);
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlRawAsync("UPDATE academic.kltn_batch SET status='Closed'");
        using var replay = await ArchiveAsync(key);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var newIntent = await ArchiveAsync(version: data.GetProperty("submissionVersion").GetUInt32());
        Assert.Equal(HttpStatusCode.Conflict, newIntent.StatusCode);
        await using (var db = MigrationDb()) await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM identity.user_role WHERE user_id={_studentId}");
        using var denied = await ArchiveAsync(key);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.DoesNotContain("Tóm tắt", await denied.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Archival_expired_attempt_claim_blocks_commit_and_late_upload_is_swept_again()
    {
        await PrepareArchivalAsync();
        _afterArchivalUpload = async key =>
        {
            if (!key.EndsWith(".pdf", StringComparison.Ordinal)) return;
            _clock.UtcNow = _clock.UtcNow.AddMinutes(11);
            using var scope = _app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IFinalSubmissionUploadCleanup>().CleanAsync(null);
        };
        using var response = await ArchiveAsync();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(await StoredKeysAsync());
        await using var db = Db<ThesisManagementDbContext>();
        Assert.Empty(await db.FinalSubmissions.ToListAsync());
        Assert.Equal("Cleaned", (await db.FinalSubmissionUploadAttempts.SingleAsync()).State);
    }

    private sealed class ArchivalControlledStorage(IThesisSubmissionObjectStorage inner, FinalReportWorkflowIntegrationTests owner)
        : IThesisSubmissionObjectStorage, IDisposable
    {
        public async Task UploadAsync(ThesisSubmissionObjectWriteRequest request, Stream content, CancellationToken cancellationToken = default)
        {
            if (owner._beforeArchivalUpload is { } before) await before(request.ObjectKey);
            await inner.UploadAsync(request, content, cancellationToken);
            if (owner._afterArchivalUpload is { } after) await after(request.ObjectKey);
        }
        public Task<Uri> CreateDownloadUriAsync(string objectKey, TimeSpan validFor, CancellationToken cancellationToken = default)
            => inner.CreateDownloadUriAsync(objectKey, validFor, cancellationToken);
        public Task<Uri> CreateDownloadUriAsync(string objectKey, string downloadFileName, TimeSpan validFor, CancellationToken cancellationToken = default)
            => inner.CreateDownloadUriAsync(objectKey, downloadFileName, validFor, cancellationToken);
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
            => owner._failArchivalDeletion ? Task.FromException(new IOException("Injected delete failure")) : inner.DeleteAsync(objectKey, cancellationToken);
        public void Dispose() => (inner as IDisposable)?.Dispose();
    }
    private sealed class ArchivalTestLogProvider(ConcurrentQueue<string> errors) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Capture(errors);
        public void Dispose() { }
        private sealed class Capture(ConcurrentQueue<string> values) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => level >= LogLevel.Error;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
            { if (level >= LogLevel.Error && error is not null) values.Enqueue(error.ToString()); }
        }
    }
}
