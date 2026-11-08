using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Storage;

public interface IThesisUploadLifecycle
{
    Task<Guid> BeginAsync(string objectKey, CancellationToken cancellationToken = default);
    Task<Error?> SaveAsync(Guid attemptId, Func<CancellationToken, Task<Error?>> save, Error expiredError, CancellationToken cancellationToken = default);
    Task FinishAsync(Guid attemptId);
}

public interface IThesisUploadCleanup
{
    Task CleanExpiredAsync(CancellationToken cancellationToken = default);
}
