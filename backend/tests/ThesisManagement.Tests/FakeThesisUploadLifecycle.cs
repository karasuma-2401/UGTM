using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.ThesisManagement.Application.Storage;

namespace UGTM.Modules.ThesisManagement.Tests;

internal sealed class FakeThesisUploadLifecycle : IThesisUploadLifecycle
{
    public Task<Guid> BeginAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
    public Task<Error?> SaveAsync(Guid attemptId, Func<CancellationToken, Task<Error?>> save, Error expiredError, CancellationToken cancellationToken = default)
        => save(cancellationToken);
    public Task FinishAsync(Guid attemptId) => Task.CompletedTask;
}
