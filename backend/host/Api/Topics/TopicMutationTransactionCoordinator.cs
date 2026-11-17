using UGTM.BuildingBlocks.Domain;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Api.Topics;

public sealed class TopicMutationTransactionCoordinator(
    TopicCatalogDbContext topics,
    ThesisManagementDbContext thesis) : ITopicMutationTransactionCoordinator
{
    public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if (topics.ChangeTracker.HasChanges() || thesis.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Topic coordination requires clean contexts.");
        return SharedPostgresTransactionCoordinator.ExecuteConditionalAsync(
            topics, thesis, workflow, result => result.IsSuccess, cancellationToken);
    }
}
