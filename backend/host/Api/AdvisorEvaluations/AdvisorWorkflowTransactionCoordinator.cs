using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Api.AdvisorEvaluations;

/// <summary>
/// Composition-root implementation for the small set of workflows that must atomically update
/// ThesisManagement and DefenseCouncil. Module contexts retain ownership checks for their schemas.
/// </summary>
public sealed class AdvisorWorkflowTransactionCoordinator(
    ThesisManagementDbContext thesisContext,
    DefenseCouncilDbContext defenseContext) : IAdvisorWorkflowTransactionCoordinator
{
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> workflow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        // DefenseCouncil is deliberately the secondary context: if ThesisManagement cannot save
        // the group transition, the evaluation and its audit inserted first are rolled back.
        return await SharedPostgresTransactionCoordinator.ExecuteAsync(
            thesisContext, defenseContext, workflow, cancellationToken);
    }
}
