using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.AcademicPeriods.Contracts.ReviewerAssignments;
using UGTM.Modules.AcademicPeriods.Contracts.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Fact]
    public async Task Evaluation_cp3_gate_maps_bounded_rule_churn_to_conflict_and_aborts_the_scheduling_transaction()
    {
        await Cp3SubmittedAsync(); await using var scope = _app.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        // Inject only the owner's exhausted-retry outcome; all other guards use production readers/PG.
        var gate = new ReviewerDefenseEligibilityContract(services.GetRequiredService<IReviewerAssignmentThesisReader>(),
            services.GetRequiredService<IReviewerAssignmentTopicReader>(), services.GetRequiredService<IReviewerAssignmentBatchReader>(),
            services.GetRequiredService<IReviewerAssignmentApprovalReader>(), services.GetRequiredService<IReviewerAssignmentRepository>(),
            services.GetRequiredService<IReviewerEvaluationRepository>(), new ExhaustedRuleReader(services.GetRequiredService<IReviewerDefenseRuleReader>()));
        var handler = new GetReviewerDefenseEligibilityHandler(gate, services.GetRequiredService<IReviewerFeedbackGroupReader>(),
            services.GetRequiredService<IReviewerAssignmentRepository>(), services.GetRequiredService<ReviewerEvaluationAuthorization>(), services.GetRequiredService<ICurrentUser>());
        Assert.Equal("ReviewerEvaluation.ConcurrencyConflict", (await handler.HandleAsync(_group)).Error.Code);
        await Assert.ThrowsAsync<DefenseCouncilConcurrencyException>(() => services.GetRequiredService<IReviewerAssignmentTransactionCoordinator>().ExecuteAsync(async token =>
        {
            await gate.LockForSchedulingAsync(_group, token); return ReviewerAssignmentTransactionResult<bool>.Commit(true);
        }));
        await AssertEvaluationCountsAsync(1, 1, 1);
    }
    private sealed class ExhaustedRuleReader(IReviewerDefenseRuleReader actual) : IReviewerDefenseRuleReader
    {
        public Task<string?> GetBatchStatusAsync(Guid batchId, CancellationToken cancellationToken = default) => actual.GetBatchStatusAsync(batchId, cancellationToken);
        public Task<ReviewerDefenseRuleContractDto?> GetActiveAsync(Guid batchId, CancellationToken cancellationToken = default) => throw new ReviewerDefenseRuleConcurrencyException();
        public Task<ReviewerDefenseRuleContractDto?> LockActiveAsync(Guid batchId, CancellationToken cancellationToken = default) => throw new ReviewerDefenseRuleConcurrencyException();
    }
}
