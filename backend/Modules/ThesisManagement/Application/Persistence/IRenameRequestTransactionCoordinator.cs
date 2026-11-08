using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Persistence;

public interface IRenameRequestTransactionCoordinator
{
    Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> workflow,
        CancellationToken cancellationToken = default);
}
