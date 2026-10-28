using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.SubmitFinalReport;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;
using IdentityUser = UGTM.Modules.Identity.Domain.Entities.User;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed partial class FinalReportSubmissionHttpIntegrationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _partnerId = Guid.NewGuid();
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly ControlledStorage _storage = new();
    private readonly TestBatchReader _batchReader = new();
    private readonly CapturedLogs _logs = new();
    private readonly FixedTimeProvider _clock = new();
    private readonly TestAdvisorAssignments _advisorAssignments = new();
    private Exception? _failureAfterCommit;
    private Func<ThesisManagementDbContext, CancellationToken, Task>? _beforeSave;
    private Guid _groupId;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await using (var db = CreateMigrationDbContext())
        {
            await ResetSchemasAsync(db);
            await db.Database.MigrateAsync();
        }
        await SeedGroupAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(_logs);
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CONNECTION_STRING"] = fixture.ConnectionString
        });
        new ThesisManagementModule().RegisterModule(builder.Services, builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
        builder.Services.AddSingleton<IThesisSubmissionObjectStorage>(_storage);
        builder.Services.AddSingleton<IKltnBatchReader>(_batchReader);
        builder.Services.AddSingleton<ITopicAdvisorAssignmentContract>(_advisorAssignments);
        builder.Services.AddSingleton<TimeProvider>(_clock);
        builder.Services.AddScoped<IThesisUnitOfWork>(provider => new ControlledUnitOfWork(
            provider.GetRequiredService<ThesisManagementDbContext>(), () => _failureAfterCommit,
            (db, token) => _beforeSave?.Invoke(db, token) ?? Task.CompletedTask));
        builder.Services.AddAuthentication(TestAuthentication.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthentication>(TestAuthentication.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        new ThesisManagementModule().MapEndpoints(_app);
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
        Authenticate(_studentId);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        await using var db = CreateMigrationDbContext();
        await ResetSchemasAsync(db);
    }

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("doc", "application/msword")]
    [InlineData("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task Multipart_submission_saves_report_file_group_and_audit_with_trusted_metadata(string extension, string contentType)
    {
        var bytes = Document(extension);
        using var form = Form(bytes, $"report.{extension}");
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(_groupId, data.GetProperty("groupId").GetGuid());
        Assert.Equal("InitialFinal", data.GetProperty("versionType").GetString());
        Assert.Equal(1, data.GetProperty("versionNo").GetInt32());
        Assert.Equal("Submitted", data.GetProperty("status").GetString());
        Assert.Equal(Now, data.GetProperty("submittedAtUtc").GetDateTime());
        Assert.False(data.GetProperty("file").TryGetProperty("objectKey", out _));

        await using var db = CreateThesisDbContext();
        var report = await db.FinalReports.SingleAsync();
        var file = await db.SubmissionFiles.SingleAsync();
        var audit = await db.AuditOutboxMessages.SingleAsync();
        Assert.Equal(report.Id, data.GetProperty("finalReportId").GetGuid());
        Assert.Equal(file.Id, data.GetProperty("file").GetProperty("id").GetGuid());
        Assert.Equal(SubmissionEntityType.FinalReport, file.EntityType);
        Assert.Equal(report.Id, file.EntityId);
        Assert.Equal(1, file.VersionNo);
        Assert.Equal(contentType, file.ContentType);
        Assert.Equal(contentType, data.GetProperty("file").GetProperty("contentType").GetString());
        Assert.Equal(bytes.Length, file.FileSize);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), file.ChecksumSha256);
        Assert.Equal(_studentId, file.UploadedBy);
        Assert.Equal(Now, file.UploadedAt);
        Assert.Equal(StudentGroupStatus.FinalReportSubmitted, (await db.StudentGroups.SingleAsync()).Status);
        Assert.Equal(IntegrationAuditActions.FinalReportSubmitted, audit.Action);
        var integrationEvent = JsonSerializer.Deserialize<AuditIntegrationEvent>(audit.PayloadJson)!;
        Assert.Equal(_studentId, integrationEvent.ActorUserId);
        Assert.Equal(report.Id.ToString(), integrationEvent.TargetId);
        using var metadata = JsonDocument.Parse(integrationEvent.MetadataJson!);
        Assert.Equal(file.Id, metadata.RootElement.GetProperty("FileId").GetGuid());
        Assert.False(metadata.RootElement.GetProperty("IsResubmission").GetBoolean());
        Assert.Equal(bytes, _storage.Objects[file.ObjectKey]);
        Assert.StartsWith($"thesis-submissions/finalreport/{report.Id:N}/v1/", file.ObjectKey);
    }

    [Fact]
    public async Task Another_confirmed_member_can_submit_on_behalf_of_the_group()
    {
        await using (var db = CreateThesisDbContext())
        {
            var group = await db.StudentGroups.Include(g => g.Members).SingleAsync();
            Assert.True(group.ConfirmMember(_partnerId, Now));
            await db.SaveChangesAsync();
        }
        Authenticate(_partnerId);
        using var form = Form();
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verification = CreateThesisDbContext();
        Assert.Equal(_partnerId, (await verification.SubmissionFiles.SingleAsync()).UploadedBy);
    }

    [Fact]
    public async Task Missing_authentication_returns_401_before_processing_the_form()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        using var form = Form();
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoSubmissionAsync();
    }

    [Theory]
    [InlineData("outsider")]
    [InlineData("lecturer")]
    [InlineData("pending")]
    public async Task Unauthorized_membership_or_role_returns_403(string scenario)
    {
        Authenticate(scenario == "outsider" ? Guid.NewGuid() : scenario == "pending" ? _partnerId : _studentId,
            scenario == "lecturer" ? "Lecturer" : "Student");
        using var form = Form();
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNoSubmissionAsync();
    }

    [Theory]
    [InlineData("missing-group")]
    [InlineData("bad-guid")]
    [InlineData("empty-guid")]
    [InlineData("missing-file")]
    [InlineData("empty-file")]
    [InlineData("duplicate-group")]
    [InlineData("duplicate-file")]
    [InlineData("wrong-file-field")]
    public async Task Invalid_multipart_fields_return_400_without_uploading(string scenario)
    {
        using var form = new MultipartFormDataContent();
        if (scenario != "missing-group")
        {
            var groupId = scenario == "bad-guid" ? "not-a-guid" : scenario == "empty-guid" ? Guid.Empty.ToString() : _groupId.ToString();
            form.Add(new StringContent(groupId), "groupId");
            if (scenario == "duplicate-group") form.Add(new StringContent(groupId), "groupId");
        }
        if (scenario != "missing-file")
        {
            form.Add(new ByteArrayContent(scenario == "empty-file" ? [] : Document("pdf")),
                scenario == "wrong-file-field" ? "otherFile" : "file", "report.pdf");
            if (scenario == "duplicate-file") form.Add(new ByteArrayContent(Document("pdf")), "file", "other.pdf");
        }
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoSubmissionAsync();
    }

    [Theory]
    [InlineData("application/json", HttpStatusCode.UnsupportedMediaType)]
    [InlineData("multipart/form-data", HttpStatusCode.BadRequest)]
    [InlineData("multipart/form-data; boundary=broken", HttpStatusCode.BadRequest)]
    public async Task Content_type_and_multipart_errors_preserve_their_client_error_status(string contentType, HttpStatusCode expected)
    {
        using var body = new StringContent("not a multipart body");
        body.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        using var response = await _client.PostAsync("/api/thesis/final-reports", body);

        Assert.Equal(expected, response.StatusCode);
        await AssertNoSubmissionAsync();
    }

    [Theory]
    [InlineData("report.exe", true)]
    [InlineData("../report.pdf", true)]
    [InlineData("report.pdf", false)]
    [InlineData("report.docx", true)]
    public async Task Invalid_file_names_or_signatures_return_400(string fileName, bool pdfContent)
    {
        using var form = Form(pdfContent ? Document("pdf") : "fake document"u8.ToArray(), fileName);
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.InvalidFile.Code, await response.Content.ReadAsStringAsync());
        await AssertNoSubmissionAsync();
    }

    [Theory]
    [InlineData(0, HttpStatusCode.OK)]
    [InlineData(1, HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(131072, HttpStatusCode.RequestEntityTooLarge)]
    public async Task Exact_file_size_is_allowed_and_oversized_files_or_requests_return_413(int excessBytes, HttpStatusCode expected)
    {
        var bytes = new byte[ThesisSubmissionFileValidator.MaximumFileSizeBytes + excessBytes];
        "%PDF-1.7"u8.CopyTo(bytes);
        using var form = Form(bytes);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/thesis/final-reports") { Content = form };
        // Observe the early HTTP rejection without racing a full HTTP/1.1 body write against socket closure.
        request.Headers.ExpectContinue = true;
        using var response = await _client.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
        if (expected != HttpStatusCode.OK) await AssertNoSubmissionAsync();
    }

    [Fact]
    public async Task Chunked_oversized_requests_also_return_413()
    {
        var bytes = new byte[ThesisSubmissionFileValidator.MaximumFileSizeBytes + 131072];
        "%PDF-1.7"u8.CopyTo(bytes);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(_groupId.ToString()), "groupId");
        form.Add(new StreamContent(new NonSeekableStream(bytes)), "file", "report.pdf");
        Assert.Null(form.Headers.ContentLength);
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        await AssertNoSubmissionAsync();
    }

    [Fact]
    public async Task Missing_groups_return_404()
    {
        using var form = Form(groupId: Guid.NewGuid());
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertNoSubmissionAsync();
    }

    [Fact]
    public async Task A_closed_submission_window_returns_409_before_upload()
    {
        _batchReader.IsOpen = false;
        using var form = Form();
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.OutsideAllowedWindow.Code, await response.Content.ReadAsStringAsync());
        await AssertNoSubmissionAsync();
    }

    [Fact]
    public async Task Upload_failure_does_not_change_database_state_and_cleans_up_the_object()
    {
        _storage.UploadFailure = new IOException("Simulated storage failure after accepting an object.");
        using var form = Form();
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertNoSubmissionAsync(expectUpload: true);
        Assert.Single(_storage.Deletes);
    }

    [Fact]
    public async Task Audit_insert_failure_rolls_back_report_file_and_group_and_compensates_storage()
    {
        await using (var db = CreateThesisDbContext())
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE thesis.audit_outbox_message ADD CONSTRAINT force_final_report_audit_failure
                CHECK (action <> 'ThesisManagement.FinalReport.Submitted')
                """);
        }
        using var form = Form();
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertNoSubmissionAsync(expectUpload: true);
        Assert.Single(_storage.Deletes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failure_reported_after_commit_cannot_delete_the_committed_file(bool isResubmission)
    {
        if (isResubmission)
        {
            using var firstForm = Form();
            using var first = await _client.PostAsync("/api/thesis/final-reports", firstForm);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }
        _failureAfterCommit = new IOException("Simulated loss of commit acknowledgement");
        using var form = Form();
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var db = CreateThesisDbContext();
        Assert.Equal(1, await db.FinalReports.CountAsync());
        var expectedVersion = isResubmission ? 2 : 1;
        var files = await db.SubmissionFiles.ToListAsync();
        Assert.Equal(expectedVersion, files.Count);
        Assert.Equal(expectedVersion, (await db.FinalReports.SingleAsync()).CurrentVersionNo);
        Assert.Equal(expectedVersion, await db.AuditOutboxMessages.CountAsync());
        Assert.Equal(StudentGroupStatus.FinalReportSubmitted, (await db.StudentGroups.SingleAsync()).Status);
        Assert.All(files, file => Assert.True(_storage.Objects.ContainsKey(file.ObjectKey)));
        Assert.Empty(_storage.Deletes);
    }

    [Fact]
    public async Task A_group_changed_during_upload_returns_409_and_keeps_the_external_change()
    {
        _storage.AfterUpload = async _ =>
        {
            await using var db = CreateThesisDbContext();
            var group = await db.StudentGroups.SingleAsync();
            Assert.True(group.TryStop());
            await db.SaveChangesAsync();
        };
        using var form = Form();
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(FinalReportSubmissionErrors.ConcurrencyConflict.Code, await response.Content.ReadAsStringAsync());
        await AssertNoSubmissionAsync(expectUpload: true, expectedGroupStatus: StudentGroupStatus.Stopped);
    }

    [Fact]
    public async Task Cancellation_during_upload_does_not_cancel_compensation()
    {
        using var cancellation = new CancellationTokenSource();
        _storage.AfterUpload = token =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };
        var accessor = _app.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = Principal(_studentId, "Student") };
        try
        {
            using var scope = _app.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SubmitFinalReportHandler>();
            await using var content = new MemoryStream(Document("pdf"));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.HandleAsync(
                new SubmitFinalReportCommand(_groupId, "report.pdf", content), cancellation.Token));
        }
        finally
        {
            accessor.HttpContext = null;
        }

        await AssertNoSubmissionAsync(expectUpload: true);
        Assert.Single(_storage.Deletes);
        Assert.False(_storage.CleanupWasCancelled);
    }

    [Fact]
    public async Task Cleanup_failure_is_logged_and_does_not_replace_the_original_error()
    {
        _storage.UploadFailure = new IOException("Original upload failure");
        _storage.DeleteFailure = new IOException("Cleanup failure");
        using var form = Form();
        using var response = await _client.PostAsync("/api/thesis/final-reports", form);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var db = CreateThesisDbContext();
        Assert.Empty(await db.FinalReports.ToListAsync());
        Assert.Empty(await db.SubmissionFiles.ToListAsync());
        Assert.Empty(await db.AuditOutboxMessages.ToListAsync());
        Assert.Contains(_logs.Entries, log => log.Level == LogLevel.Warning
            && log.Message.Contains("cleanup must be retried", StringComparison.Ordinal)
            && log.Message.Contains(_storage.Uploads.Single().ObjectKey, StringComparison.Ordinal));
        Assert.Contains(_logs.Entries, log => ReferenceEquals(log.Exception, _storage.UploadFailure));
    }

    private void Authenticate(Guid studentId, string role = "Student")
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{studentId:N}.{role}");

    private MultipartFormDataContent Form(byte[]? bytes = null, string fileName = "report.pdf", Guid? groupId = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent((groupId ?? _groupId).ToString()), "groupId");
        var file = new ByteArrayContent(bytes ?? Document("pdf"));
        // This untrusted MIME type must never become the stored content type.
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return form;
    }

    private async Task AssertNoSubmissionAsync(bool expectUpload = false,
        StudentGroupStatus expectedGroupStatus = StudentGroupStatus.InProgress)
    {
        await using var db = CreateThesisDbContext();
        Assert.Empty(await db.FinalReports.ToListAsync());
        Assert.Empty(await db.SubmissionFiles.ToListAsync());
        Assert.Empty(await db.AuditOutboxMessages.ToListAsync());
        Assert.Equal(expectedGroupStatus, (await db.StudentGroups.SingleAsync()).Status);
        Assert.Empty(_storage.Objects);
        Assert.Equal(expectUpload ? 1 : 0, _storage.Uploads.Count);
    }

    private async Task SeedGroupAsync()
    {
        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>();
        DatabaseOptions.Configure(identityOptions, fixture.ConnectionString);
        await using (var identity = new IdentityDbContext(identityOptions.Options))
        {
            identity.Users.AddRange(
                IdentityUser.Create(_studentId, "student@test.example", "STUDENT@TEST.EXAMPLE", "Student", "SV001", null, null, AccountStatus.Active, Now),
                IdentityUser.Create(_partnerId, "partner@test.example", "PARTNER@TEST.EXAMPLE", "Partner", "SV002", null, null, AccountStatus.Active, Now));
            await identity.SaveChangesAsync();
        }
        await using var db = CreateThesisDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // The tests exercise ThesisManagement persistence; unrelated topic and batch records are omitted.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-80));
        group.AddConfirmedMember(_studentId, _batchId, Now.AddDays(-80));
        group.AddPendingMember(_partnerId, _batchId, _studentId, "test-confirmation-hash", Now.AddDays(-1), Now.AddDays(1));
        Assert.True(group.TrySubmitSyllabus());
        Assert.True(group.TryApproveSyllabus());
        Assert.True(group.TrySubmitMidtermReport());
        var midterm = MidtermReport.CreateSubmitted(Guid.NewGuid(), group.Id);
        midterm.RecordReview(ReviewDecision.Approved, null, Guid.NewGuid(), Now.AddDays(-1));
        Assert.True(group.TryApproveMidtermReport());
        db.StudentGroups.Add(group);
        db.MidtermReports.Add(midterm);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        _groupId = group.Id;
    }

    private static byte[] Document(string extension)
    {
        if (extension == "pdf") return "%PDF-1.7\nFinal report"u8.ToArray();
        if (extension == "doc") return [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00];
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in new[] { "[Content_Types].xml", "word/document.xml" })
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), Encoding.UTF8);
                writer.Write("<document />");
            }
        }
        return stream.ToArray();
    }

    private ThesisManagementDbContext CreateThesisDbContext()
    {
        var options = new DbContextOptionsBuilder<ThesisManagementDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        return new ThesisManagementDbContext(options.Options);
    }

    private UGTMDbContext CreateMigrationDbContext()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString, typeof(UGTMDbContext).Assembly.GetName().Name);
        return new UGTMDbContext(options.Options);
    }

    private static Task ResetSchemasAsync(UGTMDbContext db) => db.Database.ExecuteSqlRawAsync("""
        DROP SCHEMA IF EXISTS identity CASCADE;
        DROP SCHEMA IF EXISTS academic CASCADE;
        DROP SCHEMA IF EXISTS topics CASCADE;
        DROP SCHEMA IF EXISTS thesis CASCADE;
        DROP SCHEMA IF EXISTS councils CASCADE;
        DROP SCHEMA IF EXISTS notifications CASCADE;
        DROP SCHEMA IF EXISTS public CASCADE;
        CREATE SCHEMA public;
        """);

    private static ClaimsPrincipal Principal(Guid userId, string role, string? permission = null)
    {
        var claims = new List<Claim> { new("sub", userId.ToString()), new(ClaimTypes.Role, role) };
        if (permission is not null) claims.Add(new Claim("permission", permission));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, TestAuthentication.SchemeName));
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "FinalReportTest";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            var parts = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..].Split('.', 3) : [];
            return Task.FromResult(parts.Length >= 2 && Guid.TryParse(parts[0], out var id)
                ? AuthenticateResult.Success(new AuthenticationTicket(Principal(id, parts[1], parts.Length == 3 ? parts[2] : null), SchemeName))
                : AuthenticateResult.NoResult());
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public DateTime UtcNow { get; set; } = Now;
        public override DateTimeOffset GetUtcNow() => new(UtcNow);
    }

    private sealed class ControlledUnitOfWork(ThesisManagementDbContext db, Func<Exception?> failureAfterCommit,
        Func<ThesisManagementDbContext, CancellationToken, Task> beforeSave)
        : IThesisUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await beforeSave(db, cancellationToken);
            var result = await db.SaveChangesAsync(cancellationToken);
            if (failureAfterCommit() is { } failure) throw failure;
            return result;
        }
    }

    private sealed class ControlledStorage : IThesisSubmissionObjectStorage
    {
        public ConcurrentDictionary<string, byte[]> Objects { get; } = new();
        public ConcurrentQueue<ThesisSubmissionObjectWriteRequest> Uploads { get; } = new();
        public ConcurrentQueue<string> Deletes { get; } = new();
        public ConcurrentQueue<(string ObjectKey, TimeSpan ValidFor)> Downloads { get; } = new();
        public Exception? UploadFailure { get; set; }
        public Exception? DeleteFailure { get; set; }
        public Func<CancellationToken, Task>? AfterUpload { get; set; }
        public bool CleanupWasCancelled { get; private set; }
        public async Task UploadAsync(ThesisSubmissionObjectWriteRequest request, Stream content, CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken);
            if (!Objects.TryAdd(request.ObjectKey, memory.ToArray())) throw new InvalidOperationException("Duplicate object key.");
            Uploads.Enqueue(request);
            if (UploadFailure is not null) throw UploadFailure;
            if (AfterUpload is not null) await AfterUpload(cancellationToken);
        }
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            CleanupWasCancelled = cancellationToken.IsCancellationRequested;
            cancellationToken.ThrowIfCancellationRequested();
            Deletes.Enqueue(objectKey);
            if (DeleteFailure is not null) throw DeleteFailure;
            Objects.TryRemove(objectKey, out _);
            return Task.CompletedTask;
        }
        public Task<Uri> CreateDownloadUriAsync(string objectKey, TimeSpan validFor, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Downloads.Enqueue((objectKey, validFor));
            return Task.FromResult(new Uri($"https://storage.test/download/{Guid.NewGuid():N}?signature=test"));
        }
    }

    private sealed class TestAdvisorAssignments : ITopicAdvisorAssignmentContract
    {
        public Dictionary<Guid, Guid> Assignments { get; } = new();
        public Task<TopicAdvisorAssignmentContractDto?> GetByTopicIdAsync(Guid topicId, CancellationToken cancellationToken = default)
            => Task.FromResult(Assignments.TryGetValue(topicId, out var advisorId)
                ? new TopicAdvisorAssignmentContractDto(topicId, advisorId) : null);
    }

    private sealed class TestBatchReader : IKltnBatchReader
    {
        public bool IsOpen { get; set; } = true;
        public DateTime OpensAtUtc { get; set; } = Now.AddDays(-1);
        public DateTime DeadlineAtUtc { get; set; } = Now.AddDays(1);
        public Task<BatchFinalReportSubmissionWindowContractDto?> GetFinalReportSubmissionWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchFinalReportSubmissionWindowContractDto?>(new(batchId, "Open",
                IsOpen && nowUtc >= OpensAtUtc && nowUtc <= DeadlineAtUtc, OpensAtUtc, DeadlineAtUtc));
        public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BatchTopicPreparationWindowContractDto?> GetTopicPreparationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BatchRenameRequestWindowContractDto?> GetRenameRequestWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Entries);
        public void Dispose() { }
        private sealed class CaptureLogger(ConcurrentQueue<(LogLevel, string, Exception?)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue((logLevel, formatter(state, exception), exception));
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}
