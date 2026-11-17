using UGTM.BuildingBlocks.Domain;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Api.RenameRequests;

public sealed class RenameRequestTransactionCoordinator(
    ThesisManagementDbContext thesis,
    TopicCatalogDbContext topics) : IRenameRequestTransactionCoordinator
{
    public Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> workflow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if (thesis.ChangeTracker.HasChanges() || topics.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Rename coordination requires clean contexts.");

        return SharedPostgresTransactionCoordinator.ExecuteConditionalAsync(
            thesis, topics, workflow, result => result.IsSuccess, cancellationToken);
    }
}
