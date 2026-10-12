namespace UGTM.Modules.Identity.Application.Persistence;

public interface IIdentityUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    
    Task<TResult> ExecuteInTransaction<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}