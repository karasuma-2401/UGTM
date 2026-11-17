using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Database.Migrations;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Fact]
    public async Task Actual_council_assignment_waits_for_group_change_and_rechecks_eligibility()
    {
        await Cp3SubmittedAsync();
        var councilId = await Cp3CouncilAsync();
        await using var writer = Db<UGTMDbContext>();
        await using var transaction = await writer.Database.BeginTransactionAsync();
        var writerPid = await writer.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        await writer.Database.ExecuteSqlInterpolatedAsync(Change("group"));

        await using var scope = _app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var defense = services.GetRequiredService<DefenseCouncilDbContext>();
        await defense.Database.OpenConnectionAsync();
        var schedulingPid = await defense.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        var handler = new AssignCouncilGroupsHandler(
            services.GetRequiredService<ICouncilRepository>(),
            services.GetRequiredService<IDefenseCouncilUnitOfWork>(),
            services.GetRequiredService<IDefenseGroupContract>(),
            services.GetRequiredService<ITopicDefenseContract>(),
            services.GetRequiredService<ICouncilUserInfoReader>(),
            new Actor(_actor), services.GetRequiredService<ICouncilSchedulingGuard>());
        var scheduling = handler.HandleAsync(new AssignCouncilGroupsCommand(councilId,
            [new CouncilAssignmentInputDto(_group, 1)]));
        try { await AssertBlockedByAsync(schedulingPid, writerPid); }
        finally { await transaction.CommitAsync(); }

        var result = await scheduling.WaitAsync(TimeSpan.FromSeconds(25));
        Assert.True(result.IsFailure);
        Assert.Equal("Council.GroupNotEligibleForDefense", result.Error.Code);
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Empty(await verify.CouncilAssignments.ToListAsync());
    }

    [Theory]
    [InlineData(CouncilMemberRole.Chairman)]
    [InlineData(CouncilMemberRole.Secretary)]
    public async Task Council_leadership_role_is_unique_across_writers(CouncilMemberRole role)
    {
        var councilId = await Cp3CouncilAsync();
        await using var first = Db<DefenseCouncilDbContext>();
        first.CouncilMembers.Add(new CouncilMember { Id = Guid.NewGuid(), CouncilId = councilId, UserId = _reviewer, RoleInCouncil = role });
        await first.SaveChangesAsync();

        await using var second = Db<DefenseCouncilDbContext>();
        second.CouncilMembers.Add(new CouncilMember { Id = Guid.NewGuid(), CouncilId = councilId, UserId = _otherReviewer, RoleInCouncil = role });
        var error = await Assert.ThrowsAsync<DefenseCouncilUniqueConstraintException>(() => second.SaveChangesAsync());
        Assert.Equal("ux_council_member_leadership_role", error.ConstraintName);
    }

    [Fact]
    public async Task Council_defense_order_is_unique_even_for_different_groups()
    {
        var councilId = await Cp3CouncilAsync();
        await using var db = Db<DefenseCouncilDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
        db.CouncilAssignments.Add(new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = councilId, GroupId = Guid.NewGuid(), OrderNumber = 1 });
        await db.SaveChangesAsync();
        db.CouncilAssignments.Add(new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = councilId, GroupId = Guid.NewGuid(), OrderNumber = 1 });

        var error = await Assert.ThrowsAsync<DefenseCouncilUniqueConstraintException>(() => db.SaveChangesAsync());

        Assert.Equal("ux_council_assignment_council_order", error.ConstraintName);
        await transaction.RollbackAsync();
    }

    private async Task<Guid> Cp3CouncilAsync()
    {
        await using var db = Db<DefenseCouncilDbContext>();
        var council = new Council { Id = Guid.NewGuid(), BatchId = _batch, CouncilCode = "CP3-REPRESENTATIVE", DefenseDate = new DateOnly(2026, 10, 30), TimeSlot = "AM", Room = "A1" };
        db.Councils.Add(council); await db.SaveChangesAsync(); return council.Id;
    }
    private async Task<Guid> Cp3RuleDraftAsync(decimal minimum)
    {
        await using var db = Db<AcademicPeriodsDbContext>();
        var version = await db.ScoreWeightConfigs.Where(value => value.BatchId == _batch).MaxAsync(value => value.VersionNo) + 1;
        var config = ScoreWeightConfig.CreateDraft(Guid.NewGuid(), _batch, version, ScoreCalculationMethod.IntegerCoefficient,
            [new("NOT_A_REVIEWER_CODE", "Reviewer", ScoreComponentSource.Reviewer, ScoreComponentApplicability.Always, 1, true, minimum, true, 1)], _actor, Now, "Điều chỉnh ngưỡng cho ca kiểm thử CP3");
        db.ScoreWeightConfigs.Add(config); await db.SaveChangesAsync(); return config.Id;
    }
    private async Task<bool> Cp3ScheduleAsync(Guid councilId,
        Func<IServiceProvider, ReviewerDefenseEligibilityContractDto, CancellationToken, Task>? afterGate = null)
    {
        await using var scope = _app.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        return await services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteAsync(async token =>
        {
            var gate = await services.GetRequiredService<IReviewerDefenseEligibilityContract>().LockForSchedulingAsync(_group, token);
            if (gate is not { CanProceed: true, BatchStatus: "Open" }) return ReviewerAssignmentTransactionResult<bool>.Abort(false);
            services.GetRequiredService<DefenseCouncilDbContext>().CouncilAssignments.Add(new CouncilAssignment
            {
                Id = Guid.NewGuid(),
                CouncilId = councilId,
                GroupId = _group,
                OrderNumber = 1
            });
            if (afterGate is not null) await afterGate(services, gate, token);
            return ReviewerAssignmentTransactionResult<bool>.Commit(true);
        });
    }

    [Fact]
    public async Task Council_eligibility_uses_the_active_reviewer_rule()
    {
        await Cp3SubmittedAsync();
        await using var scope = _app.Services.CreateAsyncScope();
        var councilRepository = scope.ServiceProvider.GetRequiredService<ICouncilRepository>();

        Assert.Contains(_group, await councilRepository.GetDefenseEligibleGroupIdsAsync([_group]));
        await PublishFoundationRuleAsync(await Cp3RuleDraftAsync(6));
        Assert.DoesNotContain(_group, await councilRepository.GetDefenseEligibleGroupIdsAsync([_group]));
    }

    [Fact]
    public async Task Evaluation_cp3_scheduling_contract_requires_shared_transaction_and_rechecks_after_an_earlier_positive_get()
    {
        await Cp3SubmittedAsync(); var council = await Cp3CouncilAsync();
        await using var scope = _app.Services.CreateAsyncScope(); var gate = scope.ServiceProvider.GetRequiredService<IReviewerDefenseEligibilityContract>();
        Assert.True((await gate.GetAsync(_group))!.CanProceed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.LockForSchedulingAsync(_group));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.LockForSchedulingAsync(Guid.NewGuid()));
        await PublishFoundationRuleAsync(await Cp3RuleDraftAsync(6));
        Assert.False(await Cp3ScheduleAsync(council));
        await using var db = Db<DefenseCouncilDbContext>(); Assert.Empty(await db.CouncilAssignments.ToListAsync());
        using var current = await ReadAsync(DefenseGateRoute()); await Cp3GateAsync(current, "ScoreBelowMinimum");
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Evaluation_cp3_actual_scheduling_write_and_real_config_publisher_serialize_in_both_orders(bool publisherWins)
    {
        await Cp3SubmittedAsync(); var council = await Cp3CouncilAsync(); var newConfig = await Cp3RuleDraftAsync(6);
        await using var before = Db<DefenseCouncilDbContext>(); var oldVersion = (await before.ReviewerEvaluations.SingleAsync()).Version;
        if (publisherWins)
        {
            await Cp3PublisherWinsAsync(council, newConfig);
        }
        else
        {
            var held = Signal<int>(); var release = Signal<bool>();
            var scheduling = Cp3ScheduleAsync(council, async (services, value, token) =>
            {
                Assert.Equal(5.5m, value.MinimumReviewerScore);
                held.TrySetResult(await services.GetRequiredService<DefenseCouncilDbContext>().Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
                await release.Task.WaitAsync(TimeSpan.FromSeconds(25), token);
            });
            var backend = await held.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var publisherPid = Signal<int>(); var publish = PublishFoundationRuleAsync(newConfig, publisherPid);
            try { await AssertBlockedByAsync(await publisherPid.Task.WaitAsync(TimeSpan.FromSeconds(20)), backend); }
            finally { release.TrySetResult(true); }
            Assert.True(await scheduling.WaitAsync(TimeSpan.FromSeconds(25))); await publish.WaitAsync(TimeSpan.FromSeconds(25));
        }
        await using var after = Db<DefenseCouncilDbContext>();
        Assert.Equal(publisherWins ? 0 : 1, await after.CouncilAssignments.CountAsync());
        Assert.Equal(oldVersion, (await after.ReviewerEvaluations.SingleAsync()).Version);
        using var current = await ReadAsync(DefenseGateRoute()); var data = await Cp3GateAsync(current, "ScoreBelowMinimum");
        Assert.Equal(newConfig, data.GetProperty("scoreConfigurationId").GetGuid());
        using var feedback = await ReadAsync(FeedbackRoute(), _student, "Student"); await Cp3FeedbackAsync(feedback, "Góp ý công khai; KHÔNG kèm điểm");
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    private async Task Cp3PublisherWinsAsync(Guid council, Guid newConfig)
    {
        await using (var setup = Db<UGTMDbContext>()) await setup.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION academic.cp3_hold_retirement() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(1050301); RETURN NEW; END $$;
            CREATE TRIGGER cp3_hold_retirement AFTER UPDATE ON academic.score_weight_config
            FOR EACH ROW WHEN (OLD.status='Active' AND NEW.status='Retired') EXECUTE FUNCTION academic.cp3_hold_retirement();
            """);
        await using var blocker = Db<UGTMDbContext>(); await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1050301)");
        var publisherPid = Signal<int>(); var publishing = PublishFoundationRuleAsync(newConfig, publisherPid);
        await AssertBlockedAsync(await publisherPid.Task.WaitAsync(TimeSpan.FromSeconds(20)));
        var readerPid = Signal<int>();
        var scheduling = ScheduleAsync();
        try { await AssertBlockedByAsync(await readerPid.Task.WaitAsync(TimeSpan.FromSeconds(20)), await publisherPid.Task); }
        finally { await transaction.RollbackAsync(); }
        await publishing.WaitAsync(TimeSpan.FromSeconds(25)); Assert.False(await scheduling.WaitAsync(TimeSpan.FromSeconds(25)));

        async Task<bool> ScheduleAsync()
        {
            await using var scope = _app.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
            return await services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteAsync(async token =>
            {
                readerPid.TrySetResult(await services.GetRequiredService<DefenseCouncilDbContext>().Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
                var gate = await services.GetRequiredService<IReviewerDefenseEligibilityContract>().LockForSchedulingAsync(_group, token);
                Assert.NotNull(gate); Assert.Equal(newConfig, gate.ScoreConfigurationId);
                Assert.Equal(ReviewerDefenseEligibilityReason.ScoreBelowMinimum, gate.ReasonCode);
                if (!gate.CanProceed || gate.BatchStatus != "Open") return ReviewerAssignmentTransactionResult<bool>.Abort(false);
                services.GetRequiredService<DefenseCouncilDbContext>().CouncilAssignments.Add(new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council, GroupId = _group });
                return ReviewerAssignmentTransactionResult<bool>.Commit(true);
            });
        }
    }

    [Theory]
    [InlineData("group")]
    [InlineData("advisor")]
    [InlineData("batch")]
    [InlineData("binding")]
    [InlineData("approval")]
    [InlineData("assignment")]
    [InlineData("evaluation")]
    public async Task Evaluation_cp3_scheduling_gate_retains_owner_parent_and_evaluation_locks_until_actual_write_commit(string change)
    {
        await Cp3SubmittedAsync(); var council = await Cp3CouncilAsync(); var held = Signal<int>(); var release = Signal<bool>();
        var scheduling = Cp3ScheduleAsync(council, async (services, _, token) =>
        {
            held.TrySetResult(await services.GetRequiredService<DefenseCouncilDbContext>().Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
            await release.Task.WaitAsync(TimeSpan.FromSeconds(25), token);
        });
        var holder = await held.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await using var mutator = Db<UGTMDbContext>(); await mutator.Database.OpenConnectionAsync();
        var backend = await mutator.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        var mutation = mutator.Database.ExecuteSqlInterpolatedAsync(change switch
        {
            "approval" => $"UPDATE councils.advisor_evaluation SET score=score WHERE group_id={_group}",
            "assignment" => $"UPDATE councils.reviewer_assignment SET assigned_at=assigned_at WHERE group_id={_group}",
            "evaluation" => $"UPDATE councils.reviewer_evaluation SET comment=comment WHERE assignment_id IN (SELECT id FROM councils.reviewer_assignment WHERE group_id={_group})",
            _ => Change(change)
        });
        try { await AssertBlockedByAsync(backend, holder); } finally { release.TrySetResult(true); }
        Assert.True(await scheduling.WaitAsync(TimeSpan.FromSeconds(25))); await mutation.WaitAsync(TimeSpan.FromSeconds(25));
        await using var db = Db<DefenseCouncilDbContext>(); Assert.Single(await db.CouncilAssignments.ToListAsync());
    }

    [Fact]
    public async Task Evaluation_cp3_scheduling_preserves_final_grade_value_without_locking_or_requiring_live_reviewer_account()
    {
        await Cp3SubmittedAsync(); var council = await Cp3CouncilAsync();
        Assert.True(await Cp3ScheduleAsync(council, async (_, value, token) =>
        {
            await using var identity = Db<UGTMDbContext>();
            await identity.Database.ExecuteSqlInterpolatedAsync(Change("account"), token).WaitAsync(TimeSpan.FromSeconds(5), token);
            Assert.True(value.CanProceed);
        }));
        using var gate = await ReadAsync(DefenseGateRoute()); await Cp3GateAsync(gate);
        await AssertEvaluationCountsAsync(1, 1, 1);
    }

    [Fact]
    public async Task Evaluation_cp3_professional_gate_can_remain_positive_when_closed_but_scheduling_writer_applies_its_own_time_policy()
    {
        await Cp3SubmittedAsync(); var council = await Cp3CouncilAsync();
        await using (var raw = Db<UGTMDbContext>()) await raw.Database.ExecuteSqlInterpolatedAsync(Change("batch"));
        using var response = await ReadAsync(DefenseGateRoute()); await Cp3GateAsync(response);
        Assert.False(await Cp3ScheduleAsync(council));
        await using var db = Db<DefenseCouncilDbContext>(); Assert.Empty(await db.CouncilAssignments.ToListAsync());
    }
}
