using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Fact]
    public async Task Rubric_reads_automatically_supply_the_fixed_UIT_form_with_stable_ids()
    {
        await using var db = Db<DefenseCouncilDbContext>();
        Assert.Empty(await db.GradingCriterias.Where(c => c.BatchId == _batch).ToListAsync());
        var manager = new GradingRubricManager(db, new OpenRegistrationBatchReader(), new Actor(_actor));
        var first = await manager.GetAsync(_batch);
        Assert.True(first.IsSuccess);
        Assert.Equal(10, first.Value.Criteria.Count);
        Assert.Equal(10m, first.Value.Criteria.Sum(c => c.MaxScore));
        Assert.Equal(new[] { "1.1", "1.2", "2.1", "2.2", "2.3", "3.1", "3.2", "3.3", "4.1", "4.2" },
            first.Value.Criteria.Select(c => c.Code));
        Assert.All(first.Value.Criteria, c => Assert.NotEqual(Guid.Empty, c.Id));
        var second = await manager.GetAsync(_batch);
        Assert.Equal(first.Value.Criteria.Select(c => c.Id), second.Value.Criteria.Select(c => c.Id));
        Assert.Equal(10, await db.GradingCriterias.CountAsync(c => c.BatchId == _batch));
    }

    [Fact]
    public async Task Rubric_concurrent_initial_reads_do_not_create_duplicate_criteria()
    {
        await using var firstDb = Db<DefenseCouncilDbContext>();
        await using var secondDb = Db<DefenseCouncilDbContext>();
        var first = new GradingRubricManager(firstDb, new OpenRegistrationBatchReader(), new Actor(_actor));
        var second = new GradingRubricManager(secondDb, new OpenRegistrationBatchReader(), new Actor(_actor));
        var results = await Task.WhenAll(first.GetAsync(_batch), second.GetAsync(_batch));
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(results[0].Value.Criteria.Select(c => c.Id), results[1].Value.Criteria.Select(c => c.Id));
        Assert.Equal(10, await firstDb.GradingCriterias.CountAsync(c => c.BatchId == _batch));
    }

    [Fact]
    public async Task Rubric_can_be_replaced_before_use_and_requires_valid_total_and_role()
    {
        await using var db = Db<DefenseCouncilDbContext>();
        var manager = new GradingRubricManager(db, new OpenRegistrationBatchReader(), new Actor(_actor));
        var invalid = await manager.ReplaceAsync(_batch, [new("A", "A", 4), new("a", "B", 6)]);
        Assert.Equal("GradingRubric.Invalid", invalid.Error.Code);
        var first = await manager.ReplaceAsync(_batch, [new("a", "A", 4), new("b", "B", 6)]);
        Assert.True(first.IsSuccess);
        Assert.Equal("A", first.Value.Criteria[0].Code);
        var replacement = await manager.ReplaceAsync(_batch, [new("a", "Updated", 10)]);
        Assert.True(replacement.IsSuccess);
        Assert.NotEqual(first.Value.Criteria[0].Id, replacement.Value.Criteria[0].Id);
        Assert.False(replacement.Value.Locked);

        await using var otherDb = Db<DefenseCouncilDbContext>();
        var studentManager = new GradingRubricManager(otherDb, new OpenRegistrationBatchReader(), new RubricStudent(_student));
        Assert.Equal("GradingRubric.Forbidden", (await studentManager.GetAsync(_batch)).Error.Code);
        Assert.Equal("GradingRubric.Forbidden", (await studentManager.ReplaceAsync(_batch, [new("C", "C", 10)])).Error.Code);
    }

    [Fact]
    public async Task Rubric_replacement_waits_for_score_and_remains_locked_after_detail_removed()
    {
        Guid criterion;
        await using (var setup = Db<DefenseCouncilDbContext>())
        {
            var result = await new GradingRubricManager(setup, new OpenRegistrationBatchReader(), new Actor(_actor))
                .ReplaceAsync(_batch, [new("C", "Criterion", 10)]);
            Assert.True(result.IsSuccess);
            criterion = Assert.Single(result.Value.Criteria).Id;
        }

        await using var scorer = Db<DefenseCouncilDbContext>();
        await using var scoreTransaction = await scorer.Database.BeginTransactionAsync();
        scorer.EvaluationDetails.Add(new EvaluationDetail { Id = Guid.NewGuid(), CriteriaId = criterion, EvaluationId = Guid.NewGuid(), Score = 7 });
        await scorer.SaveChangesAsync(); // The database trigger records use in the scoring transaction.

        await using var editor = Db<DefenseCouncilDbContext>();
        await editor.Database.OpenConnectionAsync();
        var pid = await editor.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        var manager = new GradingRubricManager(editor, new OpenRegistrationBatchReader(), new Actor(_actor));
        var replacing = manager.ReplaceAsync(_batch, [new("NEW", "Replacement", 10)]);
        try { await AssertBlockedAsync(pid); }
        finally { await scoreTransaction.CommitAsync(); }
        var denied = await replacing.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal("GradingRubric.Locked", denied.Error.Code);

        await using (var delete = Db<DefenseCouncilDbContext>())
        {
            await delete.EvaluationDetails.ExecuteDeleteAsync();
        }
        await using var verify = Db<DefenseCouncilDbContext>();
        var rubric = await new GradingRubricManager(verify, new OpenRegistrationBatchReader(), new Actor(_actor)).GetAsync(_batch);
        Assert.True(rubric.Value.Locked);
        Assert.Equal(criterion, Assert.Single(rubric.Value.Criteria).Id);
    }

    [Theory]
    [InlineData("advisor")]
    [InlineData("reviewer")]
    [InlineData("council")]
    public async Task Every_scoring_repository_holds_rubric_lock_through_transaction(string kind)
    {
        await using (var setup = Db<DefenseCouncilDbContext>())
            Assert.True((await new GradingRubricManager(setup, new OpenRegistrationBatchReader(), new Actor(_actor))
                .ReplaceAsync(_batch, [new("C", "Criterion", 10)])).IsSuccess);
        await using var scope = _app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var scoringDb = services.GetRequiredService<DefenseCouncilDbContext>();
        await using var scoreTransaction = await scoringDb.Database.BeginTransactionAsync();
        var criteria = kind switch
        {
            "advisor" => await services.GetRequiredService<IAdvisorEvaluationRepository>().GetGradingCriteriaByBatchIdAsync(_batch),
            "reviewer" => await services.GetRequiredService<IReviewerEvaluationRepository>().GetGradingCriteriaByBatchIdAsync(_batch),
            _ => await services.GetRequiredService<ICouncilRepository>().GetGradingCriteriaByBatchIdAsync(_batch)
        };
        Assert.Single(criteria);

        await using var editor = Db<DefenseCouncilDbContext>();
        await editor.Database.OpenConnectionAsync();
        var pid = await editor.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        var replacing = new GradingRubricManager(editor, new OpenRegistrationBatchReader(), new Actor(_actor))
            .ReplaceAsync(_batch, [new("NEW", "Replacement", 10)]);
        try { await AssertBlockedAsync(pid); }
        finally { await scoreTransaction.CommitAsync(); }
        Assert.True((await replacing.WaitAsync(TimeSpan.FromSeconds(15))).IsSuccess);
    }

    private sealed class RubricStudent(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => id;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string> { "Student" };
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }
}
