using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.Common;

public interface ICouncilSchedulingGuard
{
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> workflow, CancellationToken cancellationToken = default);
}

public sealed class CouncilSchedulingGuard(IReviewerAssignmentTransactionCoordinator coordinator) : ICouncilSchedulingGuard
{
    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        try
        {
            return await coordinator.ExecuteAsync(async token =>
            {
                var result = await workflow(token);
                return result.IsSuccess
                    ? ReviewerAssignmentTransactionResult<Result<T>>.Commit(result)
                    : ReviewerAssignmentTransactionResult<Result<T>>.Abort(result);
            }, cancellationToken);
        }
        catch (DefenseCouncilPersistenceException)
        {
            return Result.Failure<T>(CouncilErrors.ConcurrentModification);
        }
    }
}
