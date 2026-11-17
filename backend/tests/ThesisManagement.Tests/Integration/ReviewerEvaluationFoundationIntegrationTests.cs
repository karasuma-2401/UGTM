using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Publish;
using UGTM.Modules.AcademicPeriods.Contracts.ReviewerEvaluations;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reviewer_evaluation_foundation_commits_nullable_draft_or_locked_submission_with_actual_xmin_receipt_and_audit(bool submit)
    {
        var created = await CreateAsync();
        var saved = await SaveFoundationEvaluationAsync(created.Assignment!.Id, submit);
        await using var db = Db<DefenseCouncilDbContext>();
        var sheet = await db.ReviewerEvaluations.SingleAsync();
        Assert.False(sheet.IsLegacy);
        Assert.True(sheet.Version > 0);
        Assert.Equal(saved.Version, sheet.Version);
        Assert.Equal(_actor, sheet.CreatedBy); Assert.Equal(_actor, sheet.EvaluatedBy);
        Assert.Equal(submit, sheet.IsLocked); Assert.Equal(submit, sheet.IsVisibleToStudent);
        Assert.Equal(submit ? 8.35m : null, sheet.Score);
        Assert.Equal(submit ? DefenseApprovalStatus.Rejected : null, sheet.DefenseApproval);
        var receipt = await db.ReviewerEvaluationIdempotencyRecords.SingleAsync();
        using var payload = JsonDocument.Parse(receipt.ResponsePayloadJson);
        Assert.Equal(sheet.Version, payload.RootElement.GetProperty("evaluationVersion").GetUInt32());
        var audit = await db.AuditOutboxMessages.SingleAsync(value => value.Action == (submit
            ? IntegrationAuditActions.ReviewerEvaluationSubmitted : IntegrationAuditActions.ReviewerEvaluationDraftSaved));
        var entry = JsonSerializer.Deserialize<AuditIntegrationEvent>(audit.PayloadJson)!;
        Assert.Equal(_actor, entry.ActorUserId);
        using var metadata = JsonDocument.Parse(entry.MetadataJson!);
        Assert.Equal(sheet.Version, metadata.RootElement.GetProperty("EvaluationVersion").GetUInt32());
        Assert.False(metadata.RootElement.TryGetProperty("Score", out _));
        Assert.False(metadata.RootElement.TryGetProperty("Comment", out _));
        Assert.DoesNotContain("Góp ý riêng", audit.PayloadJson);
        var parent = await db.ReviewerAssignments.Include(value => value.Evaluation).SingleAsync();
        Assert.False(parent.TryCancel(_actor, Now.AddHours(1), "Không thể hủy", false));
    }

    [Fact]
    public async Task Reviewer_evaluation_foundation_rolls_back_first_save_when_receipt_and_audit_save_fails()
    {
        var created = await CreateAsync();
        await Assert.ThrowsAsync<DefenseCouncilUniqueConstraintException>(() => SaveFoundationEvaluationAsync(created.Assignment!.Id, true, true));
        await using var db = Db<DefenseCouncilDbContext>();
        Assert.Empty(await db.ReviewerEvaluations.ToArrayAsync());
        Assert.Empty(await db.ReviewerEvaluationIdempotencyRecords.ToArrayAsync());
        await AssertCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Reviewer_evaluation_foundation_enforces_unique_assignment_fk_nullable_checks_and_optimistic_concurrency()
    {
        var created = await CreateAsync();
        var saved = await SaveFoundationEvaluationAsync(created.Assignment!.Id, false);
        await using (var duplicate = Db<DefenseCouncilDbContext>())
        {
            duplicate.ReviewerEvaluations.Add(ReviewerEvaluation.CreateDraft(Guid.NewGuid(), created.Assignment.Id, null, null, null, _actor, Now));
            await Assert.ThrowsAsync<DefenseCouncilUniqueConstraintException>(() => duplicate.SaveChangesAsync());
        }
        await using var first = Db<DefenseCouncilDbContext>();
        await using var second = Db<DefenseCouncilDbContext>();
        var winner = await first.ReviewerEvaluations.SingleAsync(); var stale = await second.ReviewerEvaluations.SingleAsync();
        Assert.True(winner.TrySaveDraft(0m, null, null, _actor, Now.AddMinutes(3)));
        await first.SaveChangesAsync();
        Assert.True(stale.TrySaveDraft(9m, "Không được ghi đè", DefenseApprovalStatus.Approved, _actor, Now.AddMinutes(4)));
        await Assert.ThrowsAsync<DefenseCouncilConcurrencyException>(() => second.SaveChangesAsync());
        await using var raw = Db<UGTMDbContext>();
        foreach (var sql in new[]
        {
            $"UPDATE councils.reviewer_evaluation SET status='Submitted', is_visible_to_student=true WHERE id='{saved.Id}'",
            $"UPDATE councils.reviewer_evaluation SET defense_approval='Unknown' WHERE id='{saved.Id}'",
            $"UPDATE councils.reviewer_evaluation SET score=11 WHERE id='{saved.Id}'",
            $"DELETE FROM councils.reviewer_assignment WHERE id='{created.Assignment.Id}'"
        }) await Assert.ThrowsAsync<Npgsql.PostgresException>(() => raw.Database.ExecuteSqlRawAsync(sql));
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Equal(0m, (await verify.ReviewerEvaluations.SingleAsync()).Score);
    }

    [Fact]
    public async Task Reviewer_evaluation_foundation_repository_refreshes_unchanged_tracking_and_rejects_lock_without_transaction()
    {
        var created = await CreateAsync(); await SaveFoundationEvaluationAsync(created.Assignment!.Id, false);
        await using var scope = _app.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var repo = services.GetRequiredService<IReviewerEvaluationRepository>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.LockByAssignmentAsync(created.Assignment.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IReviewerEvaluationIdempotencyRepository>()
            .LockRequestAsync(_actor, "ReviewerEvaluation.Save", Guid.NewGuid()));
        var tracked = await services.GetRequiredService<DefenseCouncilDbContext>().ReviewerEvaluations.SingleAsync();
        await using (var other = Db<DefenseCouncilDbContext>())
        {
            var newer = await other.ReviewerEvaluations.SingleAsync();
            Assert.True(newer.TrySaveDraft(6m, null, null, _actor, Now.AddMinutes(3))); await other.SaveChangesAsync();
        }
        await services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteAsync(async token =>
        {
            Assert.NotNull(await services.GetRequiredService<IReviewerAssignmentWorkflowContract>().LockForEvaluationAsync(created.Assignment.Id, _reviewer, token));
            var fresh = await repo.LockByAssignmentAsync(created.Assignment.Id, token);
            Assert.NotNull(fresh); Assert.NotSame(tracked, fresh); Assert.Equal(6m, fresh.Score);
            Assert.NotEqual(tracked.Version, fresh.Version);
            return ReviewerAssignmentTransactionResult<bool>.Abort(true);
        });
    }

    [Fact]
    public async Task Reviewer_evaluation_foundation_receipts_are_unique_by_actor_operation_and_key()
    {
        var created = await CreateAsync(); await SaveFoundationEvaluationAsync(created.Assignment!.Id, false);
        await using var db = Db<DefenseCouncilDbContext>();
        var original = await db.ReviewerEvaluationIdempotencyRecords.SingleAsync();
        var duplicate = ReviewerEvaluationIdempotencyRecord.Create(Guid.NewGuid(), original.ActorId, original.Operation,
            original.IdempotencyKey, new string('b', 64), 200, Now);
        db.ReviewerEvaluationIdempotencyRecords.Add(duplicate);
        await Assert.ThrowsAsync<DefenseCouncilUniqueConstraintException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        foreach (var (actor, operation) in new[] { (_reviewer, original.Operation), (original.ActorId, "OtherOperation") })
        {
            var receipt = ReviewerEvaluationIdempotencyRecord.Create(Guid.NewGuid(), actor, operation, original.IdempotencyKey,
                Checksum, 200, Now);
            receipt.SetResponsePayload("{\"evaluationVersion\":10}"); db.ReviewerEvaluationIdempotencyRecords.Add(receipt);
        }
        await db.SaveChangesAsync();
        Assert.Equal(3, await db.ReviewerEvaluationIdempotencyRecords.CountAsync());
        Assert.Equal(original.ResponsePayloadJson, (await db.ReviewerEvaluationIdempotencyRecords.SingleAsync(value => value.Id == original.Id)).ResponsePayloadJson);
    }

    [Fact]
    public async Task Reviewer_evaluation_foundation_owner_readers_filter_reviewer_source_and_confirmed_membership_without_mutation()
    {
        var configId = await SeedFoundationRuleDraftAsync(); await PublishFoundationRuleAsync(configId);
        await using var scope = _app.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IReviewerDefenseRuleReader>();
        var rule = await reader.GetActiveAsync(_batch);
        Assert.NotNull(rule); Assert.Equal(configId, rule.ConfigurationId);
        Assert.Equal(2, rule.Components.Count); Assert.All(rule.Components, component => Assert.Equal(5.5m, component.MinimumScore));
        Assert.Equal(5.5m, ReviewerDefenseRuleResolver.Resolve(rule).MinimumScore);
        Assert.Null(await reader.GetActiveAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.LockActiveAsync(_batch));
        var groups = scope.ServiceProvider.GetRequiredService<IReviewerFeedbackGroupReader>();
        var own = await groups.GetAsync(_group, _student); Assert.NotNull(own);
        Assert.True(own.IsConfirmedMember); Assert.True(own.IsContextValid); Assert.Equal(_batch, own.BatchId);
        Assert.False((await groups.GetAsync(_group, _reviewer))!.IsConfirmedMember);
        Assert.Null(await groups.GetAsync(Guid.NewGuid(), _student));
        await using (var raw = Db<UGTMDbContext>()) await raw.Database.ExecuteSqlInterpolatedAsync($"UPDATE thesis.student_group_member SET confirmation_status = 'Pending' WHERE group_id = {_group} AND student_id = {_student}");
        Assert.False((await groups.GetAsync(_group, _student))!.IsConfirmedMember);
        Assert.All(Contexts(scope.ServiceProvider), context => Assert.False(context.ChangeTracker.HasChanges()));
        await using (var raw = Db<UGTMDbContext>())
            await raw.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic.score_component_config SET applicability='Unknown' WHERE config_id={configId} AND source='Reviewer'");
        Assert.Equal(ReviewerDefenseRuleOutcome.ConfigurationInvalid, ReviewerDefenseRuleResolver.Resolve(await reader.GetActiveAsync(_batch)).Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reviewer_evaluation_foundation_active_rule_lock_serializes_with_real_publisher_in_both_orders(bool publisherWins)
    {
        var newConfig = await SeedFoundationRuleDraftAsync();
        if (publisherWins)
        {
            await AssertFoundationPublisherWinsAsync(newConfig);
            return;
        }
        var held = Signal<bool>(); var release = Signal<bool>(); var pid = Signal<int>();
        await using var firstScope = _app.Services.CreateAsyncScope(); var firstServices = firstScope.ServiceProvider;
        var coordinator = firstServices.GetRequiredService<IReviewerAssignmentTransactionCoordinator>();
        var first = coordinator.ExecuteAsync(async token =>
        {
            Assert.NotNull(await firstServices.GetRequiredService<IReviewerDefenseRuleReader>().LockActiveAsync(_batch, token));
            held.TrySetResult(true); await release.Task.WaitAsync(TimeSpan.FromSeconds(25), token);
            return ReviewerAssignmentTransactionResult<bool>.Abort(true);
        });
        await held.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            var publish = PublishFoundationRuleAsync(newConfig, pid);
            await AssertBlockedAsync(await pid.Task.WaitAsync(TimeSpan.FromSeconds(15)));
            release.TrySetResult(true); await first.WaitAsync(TimeSpan.FromSeconds(15));
            await publish.WaitAsync(TimeSpan.FromSeconds(15));
            await using var verifyScope = _app.Services.CreateAsyncScope(); var verify = verifyScope.ServiceProvider;
            await verify.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteAsync(async token =>
            {
                var rule = await verify.GetRequiredService<IReviewerDefenseRuleReader>().LockActiveAsync(_batch, token);
                Assert.NotNull(rule); Assert.Equal(newConfig, rule.ConfigurationId);
                return ReviewerAssignmentTransactionResult<bool>.Abort(true);
            });
        }
        finally { release.TrySetResult(true); await first.WaitAsync(TimeSpan.FromSeconds(15)); }
    }

    private async Task<ReviewerEvaluation> SaveFoundationEvaluationAsync(Guid assignmentId, bool submit, bool fail = false)
    {
        await using var scope = _app.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var receipts = services.GetRequiredService<IReviewerEvaluationIdempotencyRepository>();
        ReviewerEvaluationIdempotencyRecord receipt = null!;
        return await services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteWithPostSaveAsync(async token =>
        {
            var key = Guid.NewGuid(); await receipts.LockRequestAsync(_actor, "ReviewerEvaluation.Save", key, token);
            Assert.Null(await receipts.GetAsync(_actor, "ReviewerEvaluation.Save", key, token));
            Assert.NotNull(await services.GetRequiredService<IReviewerAssignmentWorkflowContract>().LockForEvaluationAsync(assignmentId, _reviewer, token));
            var sheet = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), assignmentId, null, null, null, _actor, Now.AddMinutes(1));
            if (submit) Assert.True(sheet.TrySubmit(8.345m, "Góp ý riêng", DefenseApprovalStatus.Rejected, _actor, Now.AddMinutes(2)));
            services.GetRequiredService<IReviewerEvaluationRepository>().Add(sheet);
            receipt = ReviewerEvaluationIdempotencyRecord.Create(Guid.NewGuid(), _actor, "ReviewerEvaluation.Save", key, Checksum, 201, Now);
            receipts.Add(receipt);
            return ReviewerAssignmentTransactionResult<ReviewerEvaluation>.Commit(sheet);
        }, async (sheet, token) =>
        {
            var assignment = await services.GetRequiredService<IReviewerAssignmentRepository>().GetByIdAsync(assignmentId, token);
            services.GetRequiredService<ReviewerEvaluationAudit>().AddSaved(sheet, assignment!);
            receipt.SetResponsePayload(JsonSerializer.Serialize(new { evaluationId = sheet.Id, evaluationVersion = sheet.Version }));
            if (fail)
            {
                var defense = services.GetRequiredService<DefenseCouncilDbContext>();
                var duplicateId = await defense.AuditOutboxMessages.Select(value => value.Id).FirstAsync(token);
                defense.AuditOutboxMessages.Add(UGTM.Modules.DefenseCouncil.Domain.Entities.AuditOutboxMessage.Create(duplicateId,
                    IntegrationAuditActions.ReviewerEvaluationSubmitted, "{}", Now));
            }
            return sheet;
        });
    }

    private async Task<Guid> SeedFoundationRuleDraftAsync()
    {
        await using var academic = Db<AcademicPeriodsDbContext>();
        var config = ScoreWeightConfig.CreateDraft(Guid.NewGuid(), _batch, 2, ScoreCalculationMethod.IntegerCoefficient,
            [new("R_IN", "Reviewer in", ScoreComponentSource.Reviewer, ScoreComponentApplicability.ReviewerInCouncil, 1, true, 5.5m, true, 1),
             new("R_OUT", "Reviewer out", ScoreComponentSource.Reviewer, ScoreComponentApplicability.ReviewerOutsideCouncil, 1, true, 5.5m, true, 2),
             new("OTHER", "Other", ScoreComponentSource.Custom, ScoreComponentApplicability.Always, 1, true, 9m, false, 3)],
            _actor, Now, "Quy tắc phản biện mới");
        academic.ScoreWeightConfigs.Add(config); await academic.SaveChangesAsync(); return config.Id;
    }

    private async Task PublishFoundationRuleAsync(Guid configId, TaskCompletionSource<int>? pid = null)
    {
        await using var scope = _app.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var academic = services.GetRequiredService<AcademicPeriodsDbContext>();
        await academic.Database.OpenConnectionAsync(); // Keep backend pid stable through the real handler's transaction.
        pid?.TrySetResult(await academic.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync());
        var outcome = await services.GetRequiredService<PublishScoreConfigurationHandler>().HandleAsync(new(_batch, configId));
        Assert.True(outcome.IsSuccess, outcome.Error.Code);
    }

    private async Task AssertFoundationPublisherWinsAsync(Guid newConfig)
    {
        await using var raw = Db<UGTMDbContext>();
        await raw.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION academic.cp105_hold_retirement() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(1050101); RETURN NEW; END $$;
            CREATE TRIGGER cp105_hold_retirement AFTER UPDATE ON academic.score_weight_config
            FOR EACH ROW WHEN (OLD.status = 'Active' AND NEW.status = 'Retired')
            EXECUTE FUNCTION academic.cp105_hold_retirement();
            """);
        await using var gate = Db<UGTMDbContext>();
        await using var gateTransaction = await gate.Database.BeginTransactionAsync();
        await gate.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1050101)");
        var publisherPid = Signal<int>();
        var publish = PublishFoundationRuleAsync(newConfig, publisherPid);
        var released = false;
        Task<ReviewerDefenseRuleContractDto?>? read = null;
        try
        {
            await AssertBlockedAsync(await publisherPid.Task.WaitAsync(TimeSpan.FromSeconds(15)));
            var readerPid = Signal<int>();
            read = ReadWhilePublishingAsync();
            await AssertBlockedAsync(await readerPid.Task.WaitAsync(TimeSpan.FromSeconds(15)));
            await gateTransaction.RollbackAsync();
            released = true;
            await publish.WaitAsync(TimeSpan.FromSeconds(15));
            var result = await read.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.NotNull(result); Assert.Equal(newConfig, result.ConfigurationId);
            Assert.Equal(5.5m, ReviewerDefenseRuleResolver.Resolve(result).MinimumScore);

            async Task<ReviewerDefenseRuleContractDto?> ReadWhilePublishingAsync()
            {
                await using var scope = _app.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
                return await services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteAsync(async token =>
                {
                    readerPid.TrySetResult(await services.GetRequiredService<DefenseCouncilDbContext>().Database
                        .SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
                    var rule = await services.GetRequiredService<IReviewerDefenseRuleReader>().LockActiveAsync(_batch, token);
                    return ReviewerAssignmentTransactionResult<ReviewerDefenseRuleContractDto?>.Abort(rule);
                });
            }
        }
        finally
        {
            if (!released) await gateTransaction.RollbackAsync();
            await publish.WaitAsync(TimeSpan.FromSeconds(15));
            if (read is not null) await read.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }
}
