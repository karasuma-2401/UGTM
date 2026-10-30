using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Database.Migrations;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_http_serializes_with_a_real_grading_writer_in_both_lock_orders(bool evaluationWins)
    {
        var original = await CreateForCancellationAsync();
        await using var blocker = Db<DefenseCouncilDbContext>();
        await using var gate = await blocker.Database.BeginTransactionAsync();
        var gateBackend = await blocker.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        var gradingBackend = Signal<int>();
        var gradingHeld = Signal<bool>();
        var releaseGrading = Signal<bool>();

        async Task<bool> GradeAsync(bool hold)
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            return await services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteAsync(async token =>
            {
                gradingBackend.TrySetResult(await services.GetRequiredService<DefenseCouncilDbContext>().Database
                    .SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token));
                var access = await services.GetRequiredService<IReviewerAssignmentWorkflowContract>()
                    .LockForEvaluationAsync(original.Id, _reviewer, token);
                if (access is null) return ReviewerAssignmentTransactionResult<bool>.Abort(false);
                if (hold)
                {
                    gradingHeld.TrySetResult(true);
                    await releaseGrading.Task.WaitAsync(TimeSpan.FromSeconds(20), token);
                }
                services.GetRequiredService<DefenseCouncilDbContext>().ReviewerEvaluations.Add(ReviewerEvaluation.CreateDraft(Guid.NewGuid(), original.Id, 8m, "Phiếu phản biện", DefenseApprovalStatus.Approved, _reviewer, DateTime.UtcNow));
                return ReviewerAssignmentTransactionResult<bool>.Commit(true);
            });
        }

        Task<bool> grading;
        Task<HttpResponseMessage> cancelling;
        if (evaluationWins)
        {
            grading = GradeAsync(true);
            await gradingHeld.Task.WaitAsync(TimeSpan.FromSeconds(20));
            cancelling = DeleteAsync(original.Id, original.Version);
            try { await FindAssignmentWaiterAsync(await gradingBackend.Task); }
            finally { releaseGrading.TrySetResult(true); }
        }
        else
        {
            await blocker.ReviewerAssignments.FromSqlInterpolated($"""
                SELECT a.*, a.xmin FROM councils.reviewer_assignment a WHERE a.id = {original.Id} FOR UPDATE
                """).AsNoTracking().SingleAsync();
            cancelling = DeleteAsync(original.Id, original.Version);
            var cancelBackend = await FindAssignmentWaiterAsync(gateBackend);
            grading = GradeAsync(false);
            try { await AssertBlockedByAsync(await gradingBackend.Task.WaitAsync(TimeSpan.FromSeconds(20)), cancelBackend); }
            finally { await gate.RollbackAsync(); }
        }

        using var cancelled = await cancelling.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(evaluationWins, await grading.WaitAsync(TimeSpan.FromSeconds(20)));
        if (evaluationWins) await AssertErrorAsync(cancelled, HttpStatusCode.Conflict, "EvaluationExists");
        else Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        await using var verify = Db<DefenseCouncilDbContext>();
        Assert.Equal(evaluationWins, (await verify.ReviewerAssignments.SingleAsync()).IsActive);
        Assert.Equal(evaluationWins ? 1 : 0, await verify.ReviewerEvaluations.CountAsync());
        await AssertCountsAsync(1, evaluationWins ? 1 : 2, evaluationWins ? 1 : 2);
    }

    [Fact]
    public async Task Cancellation_http_and_creation_cannot_replace_the_active_row_until_cancellation_commits()
    {
        var original = await CreateForCancellationAsync();
        await using var blocker = Db<DefenseCouncilDbContext>();
        await using var gate = await blocker.Database.BeginTransactionAsync();
        var backend = await blocker.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        await blocker.ReviewerAssignments.FromSqlInterpolated($"""
            SELECT a.*, a.xmin FROM councils.reviewer_assignment a WHERE a.id = {original.Id} FOR UPDATE
            """).AsNoTracking().SingleAsync();
        var cancelling = DeleteAsync(original.Id, original.Version);
        var key = Guid.NewGuid();
        var body = await BodyAsync(_otherReviewer);
        try
        {
            await FindAssignmentWaiterAsync(backend);
            using var tooEarly = await PostAsync(body, key);
            await AssertErrorAsync(tooEarly, HttpStatusCode.Conflict, "AlreadyAssigned");
            await AssertCountsAsync(1, 1, 1);
        }
        finally { await gate.RollbackAsync(); }
        using var cancelled = await cancelling.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        // The unsuccessful create did not consume its key; it can proceed after the cancellation commits.
        using var replacement = await PostAsync(body, key);
        Assert.Equal(HttpStatusCode.Created, replacement.StatusCode);
        await using var verify = Db<DefenseCouncilDbContext>();
        var active = await verify.ReviewerAssignments.SingleAsync(value => value.IsActive);
        Assert.NotEqual(original.Id, active.Id);
        Assert.Equal(_otherReviewer, active.ReviewerId);
        await AssertCountsAsync(2, 3, 3);
    }

    private async Task<int> FindAssignmentWaiterAsync(int blocker)
    {
        await using var observer = Db<UGTMDbContext>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            var backends = await observer.Database.SqlQuery<int>($"""
                SELECT pid AS "Value" FROM pg_stat_activity
                WHERE {blocker} = ANY(pg_blocking_pids(pid)) AND query LIKE '%councils.reviewer_assignment%'
                ORDER BY pid LIMIT 1
                """).ToListAsync(timeout.Token);
            if (backends.Count > 0) return backends[0];
            await Task.Delay(25, timeout.Token);
        }
    }

    private async Task AssertBlockedByAsync(int backend, int blocker)
    {
        await using var observer = Db<UGTMDbContext>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!await observer.Database.SqlQuery<bool>($"SELECT {blocker} = ANY(pg_blocking_pids({backend})) AS \"Value\"")
                   .SingleAsync(timeout.Token))
            await Task.Delay(25, timeout.Token);
    }
}
