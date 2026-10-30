using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UGTM.Api.Modules;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Contracts.ReviewerAssignments;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Domain.Scheduling;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.ReviewerAssignments;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class ReviewerAssignmentTransactionIntegrationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 10, 30, 5, 0, 0, DateTimeKind.Utc);
    private const string Checksum = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _reviewer = Guid.NewGuid();
    private readonly Guid _otherReviewer = Guid.NewGuid();
    private readonly Guid _advisor = Guid.NewGuid();
    private readonly Guid _student = Guid.NewGuid();
    private readonly Guid _group = Guid.NewGuid();
    private readonly Guid _topic = Guid.NewGuid();
    private readonly Guid _batch = Guid.NewGuid();
    private readonly Guid _report = Guid.NewGuid();
    private readonly Guid _file = Guid.NewGuid();
    private readonly Guid _oldFile = Guid.NewGuid();
    private WebApplication _app = null!;

    public async Task InitializeAsync()
    {
        await using var db = Db<UGTMDbContext>();
        await ResetAsync(db);
        await db.Database.MigrateAsync();
        await SeedAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CONNECTION_STRING"] = fixture.ConnectionString,
            ["S3Storage:Endpoint"] = "http://localhost:9000",
            ["S3Storage:AccessKey"] = "test-access-key",
            ["S3Storage:SecretKey"] = "test-secret-key",
            ["S3Storage:BucketName"] = "reviewer-transaction-test",
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
        builder.Services.RemoveAll<ICurrentUser>();
        builder.Services.AddScoped<ICurrentUser>(_ => new Actor(_actor));
        builder.Services.AddHttpContextAccessor();
        _app = builder.Build();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
        await using var db = Db<UGTMDbContext>();
        await ResetAsync(db);
    }

    [Fact]
    public async Task Production_composition_shares_one_backend_and_commits_assignment_receipt_xmin_and_actor_audit()
    {
        var result = await CreateAsync(afterGuard: async (services, token) =>
        {
            var backends = new List<int>();
            foreach (var context in Contexts(services))
                backends.Add(await context.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
            Assert.Single(backends.Distinct());
            Assert.All(Contexts(services), value => Assert.NotNull(value.Database.CurrentTransaction));
        });
        Assert.Equal(ReviewerAssignmentEligibilityOutcome.Succeeded, result.Outcome);
        Assert.True(result.Assignment!.Version > 0);
        Assert.Equal(_file, result.Assignment.SubmissionFileId);
        Assert.Equal(2, result.Assignment.ReportVersionNo);
        Assert.Equal(_actor, result.Assignment.AssignedBy);
        using var receipt = JsonDocument.Parse(result.Receipt!.ResponsePayloadJson);
        Assert.Equal(result.Assignment.Version, receipt.RootElement.GetProperty("version").GetUInt32());
        await using var defense = Db<DefenseCouncilDbContext>();
        var audit = JsonSerializer.Deserialize<AuditIntegrationEvent>((await defense.AuditOutboxMessages.SingleAsync()).PayloadJson)!;
        Assert.Equal(_actor, audit.ActorUserId);
        Assert.Equal(IntegrationAuditActions.ReviewerAssignmentCreated, audit.Action);
        using var auditMetadata = JsonDocument.Parse(audit.MetadataJson!);
        Assert.False(auditMetadata.RootElement.TryGetProperty("Comment", out _));
        Assert.False(auditMetadata.RootElement.TryGetProperty("Score", out _));
        await using var thesis = Db<ThesisManagementDbContext>();
        Assert.Equal(StudentGroupStatus.ReadyForReviewerAssignment, (await thesis.StudentGroups.SingleAsync()).Status);
        Assert.Equal(FinalReportStatus.UnderEvaluation, (await thesis.FinalReports.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("advisor", ReviewerAssignmentEligibilityOutcome.AdvisorConflict)]
    [InlineData("batch", ReviewerAssignmentEligibilityOutcome.BatchNotOpen)]
    [InlineData("group", ReviewerAssignmentEligibilityOutcome.NotEligible)]
    [InlineData("binding", ReviewerAssignmentEligibilityOutcome.NotEligible)]
    [InlineData("account", ReviewerAssignmentEligibilityOutcome.ReviewerNotEligible)]
    [InlineData("role", ReviewerAssignmentEligibilityOutcome.ReviewerNotEligible)]
    public async Task A_change_committed_before_guard_acquisition_is_read_fresh_and_aborts_creation(
        string change, ReviewerAssignmentEligibilityOutcome expected)
    {
        await using var writer = Db<UGTMDbContext>();
        await using var writerTransaction = await writer.Database.BeginTransactionAsync();
        await writer.Database.ExecuteSqlInterpolatedAsync(Change(change));
        var blockedBackend = Signal<int>();
        var creating = CreateAsync(beforeGuard: async (services, token) =>
        {
            var pid = await services.GetRequiredService<DefenseCouncilDbContext>().Database
                .SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token);
            blockedBackend.TrySetResult(pid);
        });
        try
        {
            await AssertBlockedAsync(await blockedBackend.Task.WaitAsync(TimeSpan.FromSeconds(15)));
        }
        finally
        {
            await writerTransaction.CommitAsync();
        }
        var result = await creating.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(expected, result.Outcome);
        await AssertCountsAsync(0, 0, 0);
    }

    [Theory]
    [InlineData("advisor")]
    [InlineData("batch")]
    [InlineData("group")]
    [InlineData("binding")]
    [InlineData("account")]
    [InlineData("role")]
    public async Task Creation_retains_every_guard_lock_until_commit_and_then_releases_waiting_writers(string change)
    {
        var guarded = Signal<bool>();
        var release = Signal<bool>();
        var creating = CreateAsync(afterGuard: async (_, token) =>
        {
            guarded.TrySetResult(true);
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
        });
        await guarded.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await using var writer = Db<UGTMDbContext>();
        await writer.Database.OpenConnectionAsync();
        var backend = await writer.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        var writing = writer.Database.ExecuteSqlInterpolatedAsync(Change(change));
        try
        {
            await AssertBlockedAsync(backend);
        }
        finally
        {
            release.TrySetResult(true);
        }
        Assert.Equal(ReviewerAssignmentEligibilityOutcome.Succeeded, (await creating.WaitAsync(TimeSpan.FromSeconds(15))).Outcome);
        await writing.WaitAsync(TimeSpan.FromSeconds(15));
        await AssertCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Two_different_requests_for_the_same_group_serialize_and_leave_exactly_one_active_assignment()
    {
        var held = Signal<bool>();
        var release = Signal<bool>();
        var first = CreateAsync(afterGuard: async (_, token) =>
        {
            held.TrySetResult(true);
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
        });
        await held.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var blocked = Signal<int>();
        var second = CreateAsync(reviewer: _otherReviewer, beforeGuard: async (services, token) =>
        {
            blocked.TrySetResult(await services.GetRequiredService<DefenseCouncilDbContext>().Database
                .SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
        });
        try { await AssertBlockedAsync(await blocked.Task.WaitAsync(TimeSpan.FromSeconds(15))); }
        finally { release.TrySetResult(true); }
        Assert.Equal(ReviewerAssignmentEligibilityOutcome.Succeeded, (await first.WaitAsync(TimeSpan.FromSeconds(15))).Outcome);
        Assert.Equal(ReviewerAssignmentEligibilityOutcome.AlreadyAssigned, (await second.WaitAsync(TimeSpan.FromSeconds(15))).Outcome);
        await AssertCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task The_same_idempotency_key_waits_for_the_receipt_instead_of_creating_or_auditing_twice()
    {
        var key = Guid.NewGuid();
        var held = Signal<bool>();
        var release = Signal<bool>();
        var first = CreateAsync(key: key, afterGuard: async (_, token) =>
        {
            held.TrySetResult(true);
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
        });
        await held.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var backend = Signal<int>();
        var second = CreateAsync(key: key, beforeRequestLock: async (services, token) =>
        {
            backend.TrySetResult(await services.GetRequiredService<DefenseCouncilDbContext>().Database
                .SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
        });
        try { await AssertBlockedAsync(await backend.Task.WaitAsync(TimeSpan.FromSeconds(15))); }
        finally { release.TrySetResult(true); }
        var original = await first.WaitAsync(TimeSpan.FromSeconds(15));
        var replay = await second.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(replay.Replayed);
        using var originalJson = JsonDocument.Parse(original.Receipt!.ResponsePayloadJson);
        using var replayJson = JsonDocument.Parse(replay.Receipt!.ResponsePayloadJson);
        Assert.Equal(originalJson.RootElement.GetProperty("assignmentId").GetGuid(), replayJson.RootElement.GetProperty("assignmentId").GetGuid());
        Assert.Equal(originalJson.RootElement.GetProperty("version").GetUInt32(), replayJson.RootElement.GetProperty("version").GetUInt32());
        Assert.Equal(original.Receipt.ResponseStatusCode, replay.Receipt.ResponseStatusCode);
        await AssertCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task An_explicit_abort_discards_tracked_assignment_receipt_and_audit_even_without_an_exception()
    {
        var result = await CreateAsync(abort: true);
        Assert.Equal(ReviewerAssignmentEligibilityOutcome.Succeeded, result.Outcome);
        await AssertCountsAsync(0, 0, 0);
    }

    [Fact]
    public async Task A_post_save_database_failure_rolls_back_the_first_save_receipt_and_outbox()
    {
        await using (var db = Db<UGTMDbContext>())
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE councils.audit_outbox_message ADD CONSTRAINT cp2_reject_audit
                CHECK (action <> 'ForcedFailure')
                """);
        await Assert.ThrowsAsync<DbUpdateException>(() => CreateAsync(postSaveFailure: true));
        await AssertCountsAsync(0, 0, 0);
        // A new scope can proceed after rollback; no leaked locks or half-written receipt remain.
        Assert.Equal(ReviewerAssignmentEligibilityOutcome.Succeeded, (await CreateAsync()).Outcome);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_and_a_real_evaluation_insert_take_the_same_assignment_lock(bool evaluationWins)
    {
        var created = await CreateAsync();
        var id = created.Assignment!.Id;
        var held = Signal<bool>();
        var release = Signal<bool>();
        var blocked = Signal<int>();

        async Task<bool> EvaluateAsync(bool hold)
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            return await services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteAsync(async token =>
            {
                if (!hold) blocked.TrySetResult(await services.GetRequiredService<DefenseCouncilDbContext>().Database
                    .SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
                var access = await services.GetRequiredService<IReviewerAssignmentWorkflowContract>().LockForEvaluationAsync(id, _reviewer, token);
                if (access is null) return ReviewerAssignmentTransactionResult<bool>.Abort(false);
                if (hold)
                {
                    held.TrySetResult(true);
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
                }
                services.GetRequiredService<DefenseCouncilDbContext>().ReviewerEvaluations.Add(new ReviewerEvaluation
                {
                    Id = Guid.NewGuid(),
                    AssignmentId = access.AssignmentId,
                    Score = 8m,
                    Comment = "Phiếu phản biện",
                    DefenseApproval = DefenseApprovalStatus.Approved,
                    EvaluatedAt = Now
                });
                return ReviewerAssignmentTransactionResult<bool>.Commit(true);
            });
        }

        async Task<bool> CancelAsync(bool hold)
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            return await services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteAsync(async token =>
            {
                if (!hold) blocked.TrySetResult(await services.GetRequiredService<DefenseCouncilDbContext>().Database
                    .SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
                var repository = services.GetRequiredService<IReviewerAssignmentRepository>();
                var assignment = await repository.LockByIdAsync(id, token);
                if (assignment is null || await repository.HasEvaluationAsync(id, token))
                    return ReviewerAssignmentTransactionResult<bool>.Abort(false);
                if (hold)
                {
                    held.TrySetResult(true);
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
                }
                Assert.True(assignment.TryCancel(_actor, Now.AddMinutes(1), "Lý do riêng tư không vào audit", false));
                services.GetRequiredService<ReviewerAssignmentAudit>().AddCancelled(assignment);
                return ReviewerAssignmentTransactionResult<bool>.Commit(true);
            });
        }

        var first = evaluationWins ? EvaluateAsync(true) : CancelAsync(true);
        await held.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var second = evaluationWins ? CancelAsync(false) : EvaluateAsync(false);
        try { await AssertBlockedAsync(await blocked.Task.WaitAsync(TimeSpan.FromSeconds(15))); }
        finally { release.TrySetResult(true); }
        Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.False(await second.WaitAsync(TimeSpan.FromSeconds(15)));
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Equal(evaluationWins, (await verify.ReviewerAssignments.SingleAsync()).IsActive);
        Assert.Equal(evaluationWins ? 1 : 0, await verify.ReviewerEvaluations.CountAsync());
        var cancelledAudit = await verify.AuditOutboxMessages.Where(value => value.Action == IntegrationAuditActions.ReviewerAssignmentCancelled)
            .SingleOrDefaultAsync();
        if (!evaluationWins)
        {
            Assert.NotNull(cancelledAudit);
            var cancelledEvent = JsonSerializer.Deserialize<AuditIntegrationEvent>(cancelledAudit.PayloadJson)!;
            using var metadata = JsonDocument.Parse(cancelledEvent.MetadataJson!);
            Assert.True(metadata.RootElement.GetProperty("HasCancellationReason").GetBoolean());
            Assert.False(metadata.RootElement.TryGetProperty("CancellationReason", out _));
            Assert.False(metadata.RootElement.TryGetProperty("Reason", out _));
        }
    }

    [Fact]
    public async Task Candidate_projection_filters_active_lecturers_and_treats_search_wildcards_as_literals()
    {
        await using (var db = Db<UGTMDbContext>())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE identity.\"user\" SET full_name = '50%_ Lecturer' WHERE id = {_otherReviewer}");
        await using var scope = _app.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IReviewerCandidateReader>();
        var page = await reader.SearchAsync(_advisor, null, 1, 1);
        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.DoesNotContain(page.Items, value => value.UserId == _advisor || value.UserId == _actor);
        var literal = await reader.SearchAsync(_advisor, "%_", 1, 20);
        Assert.Equal(_otherReviewer, Assert.Single(literal.Items).UserId);
        Assert.Empty((await reader.SearchAsync(_advisor, null, int.MaxValue, 100)).Items);
        await using (var db = Db<UGTMDbContext>())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE identity.\"user\" SET status = 'Suspended' WHERE id = {_otherReviewer}");
        Assert.Equal(1, (await reader.SearchAsync(_advisor, null, 1, 20)).TotalCount);
    }

    [Fact]
    public async Task Read_contract_exposes_only_the_active_exact_binding_and_does_not_use_the_batch_deadline_as_read_access()
    {
        var created = await CreateAsync();
        await using var scope = _app.Services.CreateAsyncScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IReviewerAssignmentWorkflowContract>();
        Assert.NotNull(await workflow.GetActiveReportAccessAsync(_reviewer, _file));
        Assert.Null(await workflow.GetActiveReportAccessAsync(_otherReviewer, _file));
        Assert.Null(await workflow.GetActiveReportAccessAsync(_reviewer, _oldFile));
        await using (var db = Db<UGTMDbContext>()) await db.Database.ExecuteSqlInterpolatedAsync(Change("batch"));
        Assert.NotNull(await workflow.GetActiveReportAccessAsync(_reviewer, _file));
        await using (var db = Db<UGTMDbContext>()) await db.Database.ExecuteSqlInterpolatedAsync(Change("advisor"));
        Assert.Null(await workflow.GetActiveReportAccessAsync(_reviewer, _file));
        Assert.Equal(201, created.Receipt!.ResponseStatusCode);
        await AssertCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Lock_contracts_reject_calls_without_the_shared_transaction()
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IReviewerAssignmentThesisReader>().LockAsync(_group));
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IReviewerAssignmentTopicReader>().LockAsync(_topic));
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IReviewerAssignmentBatchReader>().LockAsync(_batch));
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IReviewerCandidateReader>().LockAsync(_reviewer));
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IReviewerAssignmentRepository>().LockByIdAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IReviewerAssignmentIdempotencyRepository>()
            .LockRequestAsync(_actor, "Create", Guid.NewGuid()));
    }

    [Fact]
    public async Task A_reader_schema_write_is_rejected_and_does_not_commit_tracked_defense_writes()
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>()
            .ExecuteAsync(async token =>
            {
                var eligible = await services.GetRequiredService<IReviewerAssignmentWorkflowContract>().LockForCreationAsync(_group, _reviewer, token);
                var binding = Assert.IsType<ReviewerAssignmentBindingContractDto>(eligible.Binding);
                var assignment = ReviewerAssignment.Create(Guid.NewGuid(), _group, _reviewer, binding.AdvisorId,
                    binding.AdvisorEvaluationId, binding.FinalReportId, binding.ReportVersionNo,
                    binding.SubmissionFileId, binding.SubmissionChecksumSha256, _actor, Now);
                services.GetRequiredService<IReviewerAssignmentRepository>().Add(assignment);
                services.GetRequiredService<IReviewerAssignmentIdempotencyRepository>().Add(
                    ReviewerAssignmentIdempotencyRecord.Create(Guid.NewGuid(), _actor, "Create", Guid.NewGuid(), Checksum, 201, Now));
                services.GetRequiredService<ReviewerAssignmentAudit>().AddCreated(assignment);
                var topic = await services.GetRequiredService<TopicCatalogDbContext>().Topics.SingleAsync(token);
                topic.TitleVi = "Must not be committed";
                return ReviewerAssignmentTransactionResult<bool>.Commit(true);
            }));
        await using var verify = Db<TopicCatalogDbContext>();
        Assert.Equal("Reviewer transaction", (await verify.Topics.SingleAsync()).TitleVi);
        await AssertCountsAsync(0, 0, 0);
        Assert.All(Contexts(services), value => Assert.Null(value.Database.CurrentTransaction));
        Assert.All(Contexts(services), value => Assert.False(value.ChangeTracker.HasChanges()));
    }

    [Fact]
    public async Task A_real_lock_timeout_rolls_back_and_is_reported_as_a_workflow_concurrency_conflict()
    {
        await using var writer = Db<UGTMDbContext>();
        await using var transaction = await writer.Database.BeginTransactionAsync();
        await writer.Database.ExecuteSqlInterpolatedAsync(Change("group"));
        await using var scope = _app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await Assert.ThrowsAsync<DefenseCouncilConcurrencyException>(() => services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>()
            .ExecuteAsync(async token =>
            {
                await services.GetRequiredService<DefenseCouncilDbContext>().Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '100ms'", token);
                await services.GetRequiredService<IReviewerAssignmentWorkflowContract>().LockForCreationAsync(_group, _reviewer, token);
                return ReviewerAssignmentTransactionResult<bool>.Commit(true);
            }));
        await transaction.RollbackAsync();
        Assert.All(Contexts(services), value => Assert.Null(value.Database.CurrentTransaction));
        await AssertCountsAsync(0, 0, 0);
    }

    private async Task<Creation> CreateAsync(Guid? reviewer = null, Guid? key = null, bool abort = false,
        bool postSaveFailure = false, Func<IServiceProvider, CancellationToken, Task>? beforeGuard = null,
        Func<IServiceProvider, CancellationToken, Task>? afterGuard = null,
        Func<IServiceProvider, CancellationToken, Task>? beforeRequestLock = null)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipts = services.GetRequiredService<IReviewerAssignmentIdempotencyRepository>();
        var requestKey = key ?? Guid.NewGuid();
        return await services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteWithPostSaveAsync(async token =>
        {
            if (beforeRequestLock is not null) await beforeRequestLock(services, token);
            await receipts.LockRequestAsync(_actor, "Create", requestKey, token);
            var prior = await receipts.GetAsync(_actor, "Create", requestKey, token);
            if (prior is not null)
                return ReviewerAssignmentTransactionResult<Creation>.Commit(new(ReviewerAssignmentEligibilityOutcome.Succeeded, Receipt: prior, Replayed: true));
            if (beforeGuard is not null) await beforeGuard(services, token);
            var eligible = await services.GetRequiredService<IReviewerAssignmentWorkflowContract>()
                .LockForCreationAsync(_group, reviewer ?? _reviewer, token);
            if (eligible.Binding is not { } binding)
                return ReviewerAssignmentTransactionResult<Creation>.Abort(new(eligible.Outcome));
            if (afterGuard is not null) await afterGuard(services, token);
            var assignment = ReviewerAssignment.Create(Guid.NewGuid(), _group, reviewer ?? _reviewer, binding.AdvisorId,
                binding.AdvisorEvaluationId, binding.FinalReportId, binding.ReportVersionNo, binding.SubmissionFileId,
                binding.SubmissionChecksumSha256, _actor, Now);
            services.GetRequiredService<IReviewerAssignmentRepository>().Add(assignment);
            var receipt = ReviewerAssignmentIdempotencyRecord.Create(Guid.NewGuid(), _actor, "Create", requestKey, Checksum, 201, Now);
            receipts.Add(receipt);
            services.GetRequiredService<ReviewerAssignmentAudit>().AddCreated(assignment);
            var result = new Creation(eligible.Outcome, assignment, receipt);
            return abort ? ReviewerAssignmentTransactionResult<Creation>.Abort(result)
                : ReviewerAssignmentTransactionResult<Creation>.Commit(result);
        }, (result, _) =>
        {
            if (!result.Replayed)
            {
                result.Receipt!.SetResponsePayload(JsonSerializer.Serialize(new { assignmentId = result.Assignment!.Id, version = result.Assignment.Version }));
                if (postSaveFailure)
                    services.GetRequiredService<DefenseCouncilDbContext>().AuditOutboxMessages.Add(
                        UGTM.Modules.DefenseCouncil.Domain.Entities.AuditOutboxMessage.Create(Guid.NewGuid(), "ForcedFailure", "{}", Now));
            }
            return Task.FromResult(result);
        });
    }

    private FormattableString Change(string change) => change switch
    {
        "advisor" => $"UPDATE topics.topic SET advisor_id = {_reviewer} WHERE id = {_topic}",
        "batch" => $"UPDATE academic.kltn_batch SET status = 'Closed' WHERE id = {_batch}",
        "group" => $"UPDATE thesis.student_group SET status = 'Stopped' WHERE id = {_group}",
        "binding" => $"UPDATE thesis.submission_file SET checksum_sha256 = {new string('b', 64)} WHERE id = {_file}",
        "account" => $"UPDATE identity.\"user\" SET status = 'Suspended' WHERE id = {_reviewer}",
        "role" => $"DELETE FROM identity.user_role WHERE user_id = {_reviewer} AND role_id = (SELECT id FROM identity.role WHERE role_name = 'Lecturer')",
        _ => throw new ArgumentOutOfRangeException(nameof(change))
    };

    private async Task AssertBlockedAsync(int backend)
    {
        await using var observer = Db<UGTMDbContext>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!await observer.Database.SqlQuery<bool>($"SELECT cardinality(pg_blocking_pids({backend})) > 0 AS \"Value\"")
                   .SingleAsync(timeout.Token))
            await Task.Delay(25, timeout.Token);
    }

    private async Task AssertCountsAsync(int assignments, int receipts, int audits)
    {
        await using var db = Db<DefenseCouncilDbContext>();
        Assert.Equal(assignments, await db.ReviewerAssignments.CountAsync());
        Assert.Equal(receipts, await db.ReviewerAssignmentIdempotencyRecords.CountAsync());
        Assert.Equal(audits, await db.AuditOutboxMessages.CountAsync());
    }

    private static DbContext[] Contexts(IServiceProvider services)
        => [services.GetRequiredService<DefenseCouncilDbContext>(), services.GetRequiredService<ThesisManagementDbContext>(),
            services.GetRequiredService<TopicCatalogDbContext>(), services.GetRequiredService<AcademicPeriodsDbContext>(),
            services.GetRequiredService<IdentityDbContext>()];

    private static TaskCompletionSource<T> Signal<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed record Creation(ReviewerAssignmentEligibilityOutcome Outcome, ReviewerAssignment? Assignment = null,
        ReviewerAssignmentIdempotencyRecord? Receipt = null, bool Replayed = false);

    private sealed class Actor(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string> { "Admin" };
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private async Task SeedAsync()
    {
        await using (var identity = Db<IdentityDbContext>())
        {
            identity.Users.AddRange(new[] { _actor, _reviewer, _otherReviewer, _advisor, _student }.Select((id, index) => User.Create(
                id, $"cp2-{index}@test.example", $"CP2-{index}@TEST.EXAMPLE", $"Lecturer {index}", $"CP2-{index}", null, null,
                AccountStatus.Active, Now)));
            var lecturerRole = await identity.Roles.SingleAsync(value => value.RoleName == "Lecturer");
            identity.UserRoles.AddRange(new[] { _reviewer, _otherReviewer, _advisor }.Select(id => new UserRole
            {
                UserId = id,
                RoleId = lecturerRole.Id,
                AssignedAt = Now
            }));
            await identity.SaveChangesAsync();
        }
        StageTemplateInput Stage(int order, string code, int from, int to) => new(order, code, code, from, to, new TimeOnly(23, 59));
        var template = ScheduleTemplateVersion.CreateDraft(Guid.NewGuid(), "REVIEWER_TRANSACTION", 1, "Reviewer transaction",
            KltnBatch.DefaultTimeZoneId,
            [Stage(1, "TOPIC_PREPARATION", 0, 6), Stage(2, "REGISTRATION_AND_SYLLABUS", 7, 13),
             Stage(3, "PROGRESS_EXECUTION", 14, 62), Stage(4, "MIDTERM_GATE", 63, 69), Stage(5, "FINAL_EVALUATION", 70, 125),
             Stage(6, "DEFENSE", 126, 139), Stage(7, "ARCHIVAL_AND_PUBLICATION", 140, 146)], _actor, Now);
        template.Publish(_actor, Now);
        var start = new DateOnly(2026, 1, 1);
        var batch = KltnBatch.Create(_batch, "Reviewer transaction", "2026-2027", 1, start, start.AddDays(146), template, _actor, Now);
        var scores = ScoreWeightConfig.CreateDraft(Guid.NewGuid(), _batch, 1, ScoreCalculationMethod.IntegerCoefficient,
            [new ScoreComponentDefinition("COUNCIL", "Council", ScoreComponentSource.CouncilAverage,
                ScoreComponentApplicability.Always, 1, true, null, false, 1)], _actor, Now);
        scores.Publish(_actor, Now);
        batch.Open(scores, _actor, Now);
        await using (var academic = Db<AcademicPeriodsDbContext>())
        {
            academic.ScheduleTemplateVersions.Add(template);
            academic.KltnBatches.Add(batch);
            academic.ScoreWeightConfigs.Add(scores);
            await academic.SaveChangesAsync();
        }
        await using (var topics = Db<TopicCatalogDbContext>())
        {
            topics.Topics.Add(new Topic
            {
                Id = _topic,
                BatchId = _batch,
                AdvisorId = _advisor,
                TitleVi = "Reviewer transaction",
                Status = TopicStatus.Published,
                StudentNumber = 1,
                CreatedAt = Now
            });
            await topics.SaveChangesAsync();
        }
        var group = new StudentGroup(_group, _topic, Now);
        group.AddConfirmedMember(_student, _batch, Now);
        Assert.True(group.TrySubmitSyllabus());
        Assert.True(group.TryApproveSyllabus());
        Assert.True(group.TrySubmitMidtermReport());
        Assert.True(group.TryApproveMidtermReport());
        Assert.True(group.TrySubmitFinalReport());
        var report = FinalReport.CreateSubmitted(_report, _group, FinalReportVersionType.InitialFinal, Now);
        Assert.True(report.TrySubmitRevision(Now));
        Assert.True(report.TryBeginEvaluation());
        Assert.True(group.TryMakeReadyForReviewerAssignment());
        await using (var thesis = Db<ThesisManagementDbContext>())
        {
            thesis.StudentGroups.Add(group);
            thesis.FinalReports.Add(report);
            thesis.SubmissionFiles.AddRange(new[] { (_oldFile, 1), (_file, 2) }.Select(pair => new SubmissionFile
            {
                Id = pair.Item1,
                EntityId = _report,
                EntityType = SubmissionEntityType.FinalReport,
                VersionNo = pair.Item2,
                FileName = "report.pdf",
                ObjectKey = $"cp2/{pair.Item1:N}",
                ContentType = "application/pdf",
                FileExtension = ".pdf",
                FileSize = 100,
                ChecksumSha256 = Checksum,
                UploadedBy = _student,
                UploadedAt = Now
            }));
            await thesis.SaveChangesAsync();
        }
        await using var defense = Db<DefenseCouncilDbContext>();
        var evaluation = AdvisorEvaluation.Create(Guid.NewGuid(), _group, _report, 2, _file, Checksum,
            8m, "Nhận xét bí mật", _advisor, Now);
        Assert.True(evaluation.TryDecide(true, null, _advisor, Now));
        defense.AdvisorEvaluations.Add(evaluation);
        await defense.SaveChangesAsync();
    }

    private T Db<T>() where T : DbContext
    {
        var options = new DbContextOptionsBuilder<T>();
        DatabaseOptions.Configure(options, fixture.ConnectionString, typeof(UGTMDbContext).Assembly.GetName().Name);
        return (T)Activator.CreateInstance(typeof(T), options.Options)!;
    }

    private static Task ResetAsync(UGTMDbContext db) => db.Database.ExecuteSqlRawAsync("""
        DROP SCHEMA IF EXISTS identity CASCADE;
        DROP SCHEMA IF EXISTS academic CASCADE;
        DROP SCHEMA IF EXISTS topics CASCADE;
        DROP SCHEMA IF EXISTS thesis CASCADE;
        DROP SCHEMA IF EXISTS councils CASCADE;
        DROP SCHEMA IF EXISTS notifications CASCADE;
        DROP SCHEMA IF EXISTS public CASCADE;
        CREATE SCHEMA public;
        """);
}
