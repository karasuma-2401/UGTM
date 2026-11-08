using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.TopicCatalog.Application.Persistence;

public interface ITopicMutationTransactionCoordinator
{
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> workflow, CancellationToken cancellationToken = default);
}
