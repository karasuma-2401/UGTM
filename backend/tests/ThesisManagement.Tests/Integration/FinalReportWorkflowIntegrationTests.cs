using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Api.Modules;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Domain.Scheduling;
using UGTM.Modules.AcademicPeriods.Infrastructure;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Infrastructure;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;
using IdentityUser = UGTM.Modules.Identity.Domain.Entities.User;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

// Unlike the focused HTTP tests, this host uses real batch/advisor readers, unit of work and S3 storage.
// Only authentication and the business clock are controlled by the test.
[Collection(PostgreSqlCollection.Name)]
public sealed class FinalReportWorkflowIntegrationTests(PostgreSqlFixture database, MinioFixture minio)
    : IClassFixture<MinioFixture>, IAsyncLifetime
{
    private static readonly DateTime SeedTime = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly BatchStart = new(2026, 9, 7);
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _partnerId = Guid.NewGuid();
    private readonly Guid _advisorId = Guid.NewGuid();
    private readonly Guid _replacementAdvisorId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly string _bucket = $"final-report-workflow-{Guid.NewGuid():N}";
    private readonly WorkflowClock _clock = new();
    private Guid _groupId;
    private Guid _topicId;
    private Guid _batchId;
    private DateTime _opensAtUtc;
    private DateTime _deadlineAtUtc;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await using (var db = MigrationDb())
        {
            await ResetSchemasAsync(db);
            await db.Database.MigrateAsync();
        }
        await SeedAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CONNECTION_STRING"] = database.ConnectionString,
            ["S3Storage:Endpoint"] = minio.Endpoint,
            ["S3Storage:AccessKey"] = "test-access-key",
            ["S3Storage:SecretKey"] = "test-secret-key",
            ["S3Storage:BucketName"] = _bucket,
            ["S3Storage:Region"] = "us-east-1",
            ["ASPNETCORE_ENVIRONMENT"] = Environments.Development,
            ["Identity:Jwt:Issuer"] = "workflow-test",
            ["Identity:Jwt:Audience"] = "workflow-test-client",
            ["Identity:Jwt:PrivateKeyPath"] = "workflow-test-private-key.pem",
            ["Identity:Jwt:KeyId"] = "workflow-test-key",
            ["Identity:Jwt:AccessTokenMinutes"] = "10",
            ["Identity:Email:PublicWebBaseUrl"] = "https://workflow.test",
            ["Identity:Email:OutboxPartition"] = "workflow-test",
            ["Identity:ClientMetadata:IpHashKey"] = "workflow-test-client-metadata-hash-key",
            ["Identity:ClientMetadata:UserAgentMaxLength"] = "300",
            ["Notifications:Smtp:Host"] = "localhost",
            ["Notifications:Smtp:Port"] = "1025",
            ["Notifications:Smtp:FromAddress"] = "workflow@test.example",
            ["Notifications:Smtp:FromName"] = "Workflow Test",
            ["Notifications:Smtp:Security"] = "None",
            ["Notifications:Smtp:TimeoutSeconds"] = "5"
        });
        builder.Services.AddUgtmModules(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
        builder.Services.AddSingleton<TimeProvider>(_clock);
        builder.Services.AddAuthentication(WorkflowAuthentication.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, WorkflowAuthentication>(WorkflowAuthentication.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapUgtmModules();
        await _app.Services.GetRequiredService<IAmazonS3>().PutBucketAsync(new PutBucketRequest { BucketName = _bucket });
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            try
            {
                var s3 = _app.Services.GetRequiredService<IAmazonS3>();
                var objects = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket });
                foreach (var item in objects.S3Objects ?? []) await s3.DeleteObjectAsync(_bucket, item.Key);
                await s3.DeleteBucketAsync(_bucket);
            }
            finally
            {
                await _app.DisposeAsync();
            }
        }
        await using var db = MigrationDb();
        await ResetSchemasAsync(db);
    }

    [Fact]
    public async Task Real_workflow_preserves_all_formats_and_versions_then_locks_the_reviewed_file()
    {
        _clock.UtcNow = _opensAtUtc.AddTicks(-1);
        using var tooEarly = await SubmitAsync("pdf", _studentId);
        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);
        Assert.Empty(await StoredKeysAsync());
        _clock.UtcNow = _opensAtUtc;
        foreach (var extension in new[] { "pdf", "doc", "docx" })
        {
            using var submitted = await SubmitAsync(extension, extension == "doc" ? _partnerId : _studentId);
            Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
            _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        }
        await using var db = Db<ThesisManagementDbContext>();
        var report = await db.FinalReports.SingleAsync();
        var files = await db.SubmissionFiles.OrderBy(file => file.VersionNo).ToListAsync();
        Assert.Equal(new[] { 1, 2, 3 }, files.Select(file => file.VersionNo));
        Assert.Equal(3, report.CurrentVersionNo);
        Assert.Equal(3, (await StoredKeysAsync()).Count);
        foreach (var file in files)
        {
            var bytes = Document(file.FileExtension.TrimStart('.'));
            Assert.Equal(bytes.Length, file.FileSize);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), file.ChecksumSha256);
            using var anonymous = new HttpClient();
            using var denied = await anonymous.GetAsync($"{minio.Endpoint}/{_bucket}/{file.ObjectKey}");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            await AssertDownloadAsync(file.Id, bytes, _studentId, "Student");
        }
        using var history = await SendAsync(HttpMethod.Get, HistoryRoute, _advisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using var json = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        var versions = json.RootElement.GetProperty("data").GetProperty("versions").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, versions.Select(version => version.GetProperty("versionNo").GetInt32()));
        Assert.All(versions, version => Assert.False(version.GetProperty("file").TryGetProperty("objectKey", out _)));

        _clock.UtcNow = _deadlineAtUtc.AddTicks(1);
        using var tooLate = await SubmitAsync("pdf", _studentId);
        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode);
        using var stale = await BeginAsync(report.Id, 2, _advisorId);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var started = await BeginAsync(report.Id, 3, _advisorId);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        using var lockedJson = JsonDocument.Parse(await started.Content.ReadAsStringAsync());
        Assert.Equal(files[2].Id, lockedJson.RootElement.GetProperty("data").GetProperty("file").GetProperty("id").GetGuid());
        // Return to the submission window to prove the evaluation lock, independently of the deadline.
        _clock.UtcNow = _opensAtUtc.AddHours(1);
        using var lockedSubmission = await SubmitAsync("pdf", _partnerId);
        Assert.Equal(HttpStatusCode.Conflict, lockedSubmission.StatusCode);
        Assert.Contains("FinalReport.EvaluationStarted", await lockedSubmission.Content.ReadAsStringAsync());
        await db.Entry(report).ReloadAsync();
        Assert.Equal(FinalReportStatus.UnderEvaluation, report.Status);
        Assert.Equal(3, report.CurrentVersionNo);
        Assert.Equal(4, await db.AuditOutboxMessages.CountAsync());
        Assert.Equal(1, await db.AuditOutboxMessages.CountAsync(row => row.Action == IntegrationAuditActions.FinalReportEvaluationStarted));
        Assert.Equal(3, (await StoredKeysAsync()).Count);
        await AssertDownloadAsync(files[0].Id, Document("pdf"), _advisorId, "Lecturer");
    }

    [Fact]
    public async Task Persisted_batch_closure_and_advisor_reassignment_are_used_by_the_http_endpoints()
    {
        using var submitted = await SubmitAsync("pdf", _studentId);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        await using var db = Db<ThesisManagementDbContext>();
        var report = await db.FinalReports.SingleAsync();
        var file = await db.SubmissionFiles.SingleAsync();
        await using (var topics = Db<TopicCatalogDbContext>())
        {
            (await topics.Topics.SingleAsync()).AdvisorId = _replacementAdvisorId;
            await topics.SaveChangesAsync();
        }
        await using (var academic = Db<AcademicPeriodsDbContext>())
        {
            var batch = await academic.KltnBatches.SingleAsync(value => value.Id == _batchId);
            batch.Close(_advisorId, _clock.UtcNow);
            await academic.SaveChangesAsync();
        }
        using var closedSubmission = await SubmitAsync("doc", _partnerId);
        Assert.Equal(HttpStatusCode.Conflict, closedSubmission.StatusCode);
        Assert.Contains("FinalReport.OutsideAllowedWindow", await closedSubmission.Content.ReadAsStringAsync());
        using var oldHistory = await SendAsync(HttpMethod.Get, HistoryRoute, _advisorId, "Lecturer");
        using var oldDownload = await SendAsync(HttpMethod.Get, DownloadRoute(file.Id), _advisorId, "Lecturer");
        using var oldStart = await BeginAsync(report.Id, 1, _advisorId);
        Assert.Equal(HttpStatusCode.Forbidden, oldHistory.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, oldDownload.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, oldStart.StatusCode);
        using var newHistory = await SendAsync(HttpMethod.Get, HistoryRoute, _replacementAdvisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.OK, newHistory.StatusCode);
        await AssertDownloadAsync(file.Id, Document("pdf"), _replacementAdvisorId, "Lecturer");
        using var newStart = await BeginAsync(report.Id, 1, _replacementAdvisorId);
        Assert.Equal(HttpStatusCode.OK, newStart.StatusCode);
        Assert.Single(await StoredKeysAsync());
    }

    [Fact]
    public async Task Database_failure_compensates_the_real_minio_upload_and_keeps_the_previous_file()
    {
        using var submitted = await SubmitAsync("pdf", _studentId);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        await using var db = Db<ThesisManagementDbContext>();
        var originalFile = await db.SubmissionFiles.SingleAsync();
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE thesis.audit_outbox_message ADD CONSTRAINT force_workflow_submission_failure
            CHECK (action <> 'ThesisManagement.FinalReport.Submitted') NOT VALID
            """);

        using var revision = await SubmitAsync("docx", _partnerId);

        Assert.Equal(HttpStatusCode.InternalServerError, revision.StatusCode);
        Assert.Equal(new[] { originalFile.ObjectKey }, await StoredKeysAsync());
        Assert.Equal(1, (await db.FinalReports.SingleAsync()).CurrentVersionNo);
        Assert.Equal(FinalReportStatus.Submitted, (await db.FinalReports.SingleAsync()).Status);
        Assert.Single(await db.SubmissionFiles.ToListAsync());
        Assert.Equal(1, await db.AuditOutboxMessages.CountAsync());
        await AssertDownloadAsync(originalFile.Id, Document("pdf"), _studentId, "Student");
    }

    [Fact]
    public async Task Real_workflow_hands_off_only_an_approved_locked_evaluation_across_all_modules()
    {
        using var firstSubmission = await SubmitAsync("pdf", _studentId);
        using var secondSubmission = await SubmitAsync("docx", _partnerId);
        Assert.Equal(HttpStatusCode.OK, firstSubmission.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondSubmission.StatusCode);

        Guid reportId;
        Guid submissionFileId;
        string submissionChecksum;
        await using (var thesis = Db<ThesisManagementDbContext>())
        {
            var report = await thesis.FinalReports.SingleAsync();
            var file = await thesis.SubmissionFiles.SingleAsync(value => value.VersionNo == 2);
            reportId = report.Id;
            submissionFileId = file.Id;
            submissionChecksum = file.ChecksumSha256
                ?? throw new InvalidOperationException("The locked submission must have a checksum.");
        }

        using var started = await BeginAsync(reportId, 2, _advisorId);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);

        using var savedByAdmin = await SaveAdvisorEvaluationAsync(
            reportId, 2, null, 8.345m, "  Đủ điều kiện nhưng cần kiểm tra lần cuối.  ",
            Guid.NewGuid(), _adminId, "Admin");
        var savedContent = await savedByAdmin.Content.ReadAsStringAsync();
        Assert.True(savedByAdmin.StatusCode == HttpStatusCode.OK, savedContent);
        Assert.Contains("8.35", savedContent);
        var savedVersion = EvaluationVersion(savedContent);

        using var studentHistory = await SendAsync(HttpMethod.Get, HistoryRoute, _studentId, "Student");
        using var studentEvaluation = await GetAdvisorEvaluationAsync(_studentId, "Student");
        using var facultyHistory = await SendAsync(
            HttpMethod.Get, HistoryRoute, Guid.NewGuid(), "Faculty", permission: PermissionCodes.ThesisReviewGatesReview);
        using var facultyEvaluation = await GetAdvisorEvaluationAsync(
            Guid.NewGuid(), "Faculty", PermissionCodes.ThesisReviewGatesReview);
        Assert.Equal(HttpStatusCode.OK, studentHistory.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, studentEvaluation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, facultyHistory.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, facultyEvaluation.StatusCode);

        await using (var topics = Db<TopicCatalogDbContext>())
        {
            (await topics.Topics.SingleAsync()).AdvisorId = _replacementAdvisorId;
            await topics.SaveChangesAsync();
        }
        using var formerAdvisorRead = await GetAdvisorEvaluationAsync(_advisorId, "Lecturer");
        using var currentAdvisorRead = await GetAdvisorEvaluationAsync(_replacementAdvisorId, "Lecturer");
        Assert.Equal(HttpStatusCode.Forbidden, formerAdvisorRead.StatusCode);
        Assert.Equal(HttpStatusCode.OK, currentAdvisorRead.StatusCode);

        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        using var rejected = await DecideAdvisorEvaluationAsync(
            reportId, 2, savedVersion, false, "Cần xác nhận lại tài liệu tham khảo.", Guid.NewGuid(), _adminId, "Admin");
        var rejectedContent = await rejected.Content.ReadAsStringAsync();
        Assert.True(rejected.StatusCode == HttpStatusCode.OK, rejectedContent);
        var rejectedVersion = EvaluationVersion(rejectedContent);

        await using (var defense = Db<DefenseCouncilDbContext>())
        {
            var audit = await defense.AuditOutboxMessages.SingleAsync(value =>
                value.Action == IntegrationAuditActions.AdvisorEvaluationRejected);
            var auditEvent = JsonSerializer.Deserialize<AuditIntegrationEvent>(audit.PayloadJson)!;
            Assert.Equal(_adminId, auditEvent.ActorUserId);
            Assert.DoesNotContain("Cần xác nhận lại tài liệu tham khảo.", audit.PayloadJson);
        }

        using (var scope = _app.Services.CreateScope())
        {
            var eligibility = scope.ServiceProvider.GetRequiredService<IReviewerAssignmentEligibilityContract>();
            Assert.Null(await eligibility.GetReviewerAssignmentEligibilityAsync(_groupId));
        }

        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        using var revised = await SaveAdvisorEvaluationAsync(
            reportId, 2, rejectedVersion, 9m, "Đã xác nhận tài liệu tham khảo.", Guid.NewGuid(),
            _replacementAdvisorId, "Lecturer");
        var revisedContent = await revised.Content.ReadAsStringAsync();
        Assert.True(revised.StatusCode == HttpStatusCode.OK, revisedContent);
        var revisedVersion = EvaluationVersion(revisedContent);

        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        using var approved = await DecideAdvisorEvaluationAsync(
            reportId, 2, revisedVersion, true, null, Guid.NewGuid(), _replacementAdvisorId, "Lecturer");
        var approvedContent = await approved.Content.ReadAsStringAsync();
        Assert.True(approved.StatusCode == HttpStatusCode.OK, approvedContent);
        var approvedVersion = EvaluationVersion(approvedContent);
        Assert.Contains("\"isLocked\":true", approvedContent);

        using (var scope = _app.Services.CreateScope())
        {
            var eligibility = scope.ServiceProvider.GetRequiredService<IReviewerAssignmentEligibilityContract>();
            var result = await eligibility.GetReviewerAssignmentEligibilityAsync(_groupId);
            Assert.NotNull(result);
            Assert.Equal(reportId, result.FinalReportId);
            Assert.Equal(submissionFileId, result.SubmissionFileId);
            Assert.Equal(submissionChecksum, result.SubmissionChecksumSha256);
            Assert.Equal(_clock.UtcNow, result.ApprovedAtUtc);
        }

        using var editAfterApproval = await SaveAdvisorEvaluationAsync(
            reportId, 2, approvedVersion, 10m, "Không được ghi đè.", Guid.NewGuid(), _adminId, "Admin");
        Assert.Equal(HttpStatusCode.Conflict, editAfterApproval.StatusCode);
        Assert.Contains("AdvisorEvaluation.Locked", await editAfterApproval.Content.ReadAsStringAsync());

        await using var verificationThesis = Db<ThesisManagementDbContext>();
        await using var verificationDefense = Db<DefenseCouncilDbContext>();
        Assert.Equal(StudentGroupStatus.ReadyForReviewerAssignment,
            (await verificationThesis.StudentGroups.SingleAsync()).Status);
        var evaluation = await verificationDefense.AdvisorEvaluations.SingleAsync();
        Assert.True(evaluation.IsLocked);
        Assert.Equal(9m, evaluation.Score);
        Assert.Equal(_replacementAdvisorId, evaluation.DecisionBy);
        Assert.Equal(1, await verificationDefense.AuditOutboxMessages.CountAsync(value =>
            value.Action == IntegrationAuditActions.AdvisorEvaluationApproved));
        Assert.Equal(1, await verificationThesis.AuditOutboxMessages.CountAsync(value =>
            value.Action == IntegrationAuditActions.AdvisorEvaluationReviewerAssignmentApproved));
    }

    private async Task SeedAsync()
    {
        await using (var identity = Db<IdentityDbContext>())
        {
            var ids = new[] { _studentId, _partnerId, _advisorId, _replacementAdvisorId, _adminId };
            identity.Users.AddRange(ids.Select((id, index) => IdentityUser.Create(id,
                $"workflow{index}@test.example", $"WORKFLOW{index}@TEST.EXAMPLE", $"Workflow {index}",
                $"WF{index:D3}", null, null, AccountStatus.Active, SeedTime)));
            await identity.SaveChangesAsync();
        }
        StageTemplateInput Stage(int order, string code, int from, int to)
            => new(order, code, code, from, to, new TimeOnly(23, 59));
        var template = ScheduleTemplateVersion.CreateDraft(Guid.NewGuid(), "FINAL_REPORT_WORKFLOW", 1,
            "Final report workflow", KltnBatch.DefaultTimeZoneId,
            [Stage(1, "TOPIC_PREPARATION", 0, 6), Stage(2, "REGISTRATION_AND_SYLLABUS", 7, 13),
             Stage(3, "PROGRESS_EXECUTION", 14, 62), Stage(4, "MIDTERM_GATE", 63, 69),
             Stage(5, "FINAL_EVALUATION", 70, 125), Stage(6, "DEFENSE", 126, 139),
             Stage(7, "ARCHIVAL_AND_PUBLICATION", 140, 146)], _advisorId, SeedTime);
        template.Publish(_advisorId, SeedTime);
        var batch = KltnBatch.Create(Guid.NewGuid(), "Final report workflow", "2026-2027", 1,
            BatchStart, BatchStart.AddDays(146), template, _advisorId, SeedTime);
        var scoreConfig = ScoreWeightConfig.CreateDraft(Guid.NewGuid(), batch.Id, 1, ScoreCalculationMethod.IntegerCoefficient,
            [new ScoreComponentDefinition("COUNCIL", "Council", ScoreComponentSource.CouncilAverage,
                ScoreComponentApplicability.Always, 1, true, null, false, 1)], _advisorId, SeedTime);
        scoreConfig.Publish(_advisorId, SeedTime);
        batch.Open(scoreConfig, _advisorId, SeedTime);
        await using (var academic = Db<AcademicPeriodsDbContext>())
        {
            academic.ScheduleTemplateVersions.Add(template);
            academic.KltnBatches.Add(batch);
            academic.ScoreWeightConfigs.Add(scoreConfig);
            await academic.SaveChangesAsync();
        }
        _batchId = batch.Id;
        _deadlineAtUtc = batch.Stages.Single(stage => stage.StageCode == "FINAL_EVALUATION").DeadlineAtUtc;
        _opensAtUtc = BatchStart.AddDays(70).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
        _clock.UtcNow = _opensAtUtc.AddHours(1);
        _topicId = Guid.NewGuid();
        await using (var topics = Db<TopicCatalogDbContext>())
        {
            topics.Topics.Add(new Topic
            {
                Id = _topicId,
                BatchId = batch.Id,
                AdvisorId = _advisorId,
                TitleVi = "Final report workflow",
                StudentNumber = 2,
                Status = TopicStatus.Published,
                CreatedAt = SeedTime
            });
            await topics.SaveChangesAsync();
        }
        var group = new StudentGroup(Guid.NewGuid(), _topicId, SeedTime);
        group.AddConfirmedMember(_studentId, batch.Id, SeedTime);
        group.AddConfirmedMember(_partnerId, batch.Id, SeedTime);
        Assert.True(group.TrySubmitSyllabus());
        Assert.True(group.TryApproveSyllabus());
        Assert.True(group.TrySubmitMidtermReport());
        Assert.True(group.TryApproveMidtermReport());
        var midterm = MidtermReport.CreateSubmitted(Guid.NewGuid(), group.Id);
        midterm.RecordReview(ReviewDecision.Approved, null, _advisorId, SeedTime);
        await using var thesis = Db<ThesisManagementDbContext>();
        thesis.StudentGroups.Add(group);
        thesis.MidtermReports.Add(midterm);
        await thesis.SaveChangesAsync();
        _groupId = group.Id;
    }

    private string HistoryRoute => $"/api/thesis/groups/{_groupId}/final-report";
    private static string DownloadRoute(Guid fileId) => $"/api/thesis/final-report-files/{fileId}/download";
    private static string AdvisorApprovalRoute(Guid finalReportId)
        => $"/api/thesis/final-reports/{finalReportId}/approval";
    private string AdvisorEvaluationReadRoute => $"/api/thesis/groups/{_groupId}/advisor-evaluation";
    private Task<HttpResponseMessage> BeginAsync(Guid reportId, int version, Guid advisorId)
        => SendAsync(HttpMethod.Post, $"/api/thesis/final-reports/{reportId}/begin-evaluation", advisorId, "Lecturer",
            JsonContent.Create(new { expectedVersionNo = version }));

    private async Task<HttpResponseMessage> SubmitAsync(string extension, Guid studentId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(_groupId.ToString()), "groupId");
        form.Add(new ByteArrayContent(Document(extension)), "file", $"report.{extension}");
        return await SendAsync(HttpMethod.Post, "/api/thesis/final-reports", studentId, "Student", form);
    }

    private async Task<HttpResponseMessage> SaveAdvisorEvaluationAsync(
        Guid finalReportId,
        int expectedReportVersionNo,
        uint? expectedEvaluationVersion,
        decimal score,
        string comment,
        Guid idempotencyKey,
        Guid actorId,
        string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/thesis/advisor-evaluations")
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

    private async Task<HttpResponseMessage> DecideAdvisorEvaluationAsync(
        Guid finalReportId,
        int expectedReportVersionNo,
        uint expectedEvaluationVersion,
        bool allowsReviewerAssignment,
        string? rejectionReason,
        Guid idempotencyKey,
        Guid actorId,
        string role)
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

    private async Task<HttpResponseMessage> GetAdvisorEvaluationAsync(
        Guid actorId,
        string role,
        string? permission = null)
        => await SendAsync(HttpMethod.Get, AdvisorEvaluationReadRoute, actorId, role, permission: permission);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid userId, string role,
        HttpContent? content = null,
        string? permission = null)
    {
        using var request = new HttpRequestMessage(method, route) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", $"{userId:N}.{role}" + (permission is null ? string.Empty : $"|{permission}"));
        return await _client.SendAsync(request);
    }

    private static uint EvaluationVersion(string responseContent)
    {
        using var json = JsonDocument.Parse(responseContent);
        return json.RootElement.GetProperty("data").GetProperty("evaluationVersion").GetUInt32();
    }

    private async Task AssertDownloadAsync(Guid fileId, byte[] expected, Guid userId, string role)
    {
        var requestedAtUtc = DateTimeOffset.UtcNow;
        using var response = await SendAsync(HttpMethod.Get, DownloadRoute(fileId), userId, role);
        var receivedAtUtc = DateTimeOffset.UtcNow;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var url = json.RootElement.GetProperty("data").GetProperty("downloadUrl").GetString()!;
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(url).Query);
        Assert.True(int.TryParse(query["X-Amz-Expires"], out var lifetimeSeconds));
        var signedAtUtc = DateTimeOffset.ParseExact(query["X-Amz-Date"].ToString(), "yyyyMMdd'T'HHmmss'Z'",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        // Check the actual expiry, allowing a small tolerance for signing precision and SDK clock adjustment.
        Assert.InRange(signedAtUtc.AddSeconds(lifetimeSeconds), requestedAtUtc.AddMinutes(5).AddSeconds(-5),
            receivedAtUtc.AddMinutes(5).AddSeconds(5));
        Assert.Equal(_clock.UtcNow.AddMinutes(5), json.RootElement.GetProperty("data").GetProperty("expiresAtUtc").GetDateTime());
        using var anonymous = new HttpClient();
        Assert.Equal(expected, await anonymous.GetByteArrayAsync(url));
    }

    private async Task<IReadOnlyList<string>> StoredKeysAsync()
    {
        var result = await _app.Services.GetRequiredService<IAmazonS3>()
            .ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket });
        return (result.S3Objects ?? []).Select(item => item.Key).Order().ToArray();
    }

    private T Db<T>() where T : DbContext
    {
        var options = new DbContextOptionsBuilder<T>();
        DatabaseOptions.Configure(options, database.ConnectionString);
        return (T)Activator.CreateInstance(typeof(T), options.Options)!;
    }

    private UGTMDbContext MigrationDb()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(options, database.ConnectionString, typeof(UGTMDbContext).Assembly.GetName().Name);
        return new UGTMDbContext(options.Options);
    }

    private static Task ResetSchemasAsync(UGTMDbContext db) => db.Database.ExecuteSqlRawAsync("""
        DROP SCHEMA IF EXISTS identity, academic, topics, thesis, councils, notifications, public CASCADE;
        CREATE SCHEMA public;
        """);

    private static byte[] Document(string extension)
    {
        if (extension == "pdf") return "%PDF-1.7\nFinal report workflow"u8.ToArray();
        if (extension == "doc") return [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00];
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in new[] { "[Content_Types].xml", "word/document.xml" })
            {
                var entry = archive.CreateEntry(name);
                // ZIP entry timestamps are part of the bytes used for checksum/download assertions.
                entry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write("<document />");
            }
        }
        return stream.ToArray();
    }

    private sealed class WorkflowClock : TimeProvider
    {
        public DateTime UtcNow { get; set; }
        public override DateTimeOffset GetUtcNow() => new(UtcNow);
    }

    private sealed class WorkflowAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "FinalReportWorkflow";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var value = Request.Headers.Authorization.ToString();
            var token = value.StartsWith("Bearer ", StringComparison.Ordinal) ? value[7..] : string.Empty;
            var separator = token.IndexOf('.');
            if (separator < 1 || !Guid.TryParse(token[..separator], out var id))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var roleAndPermission = token[(separator + 1)..].Split('|', 2);
            if (string.IsNullOrWhiteSpace(roleAndPermission[0]))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new("sub", id.ToString()),
                new(ClaimTypes.Role, roleAndPermission[0])
            };
            if (roleAndPermission.Length == 2 && !string.IsNullOrWhiteSpace(roleAndPermission[1]))
            {
                claims.Add(new Claim("permission", roleAndPermission[1]));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
