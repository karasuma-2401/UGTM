namespace UGTM.Modules.AcademicPeriods.Application.Persistence;

public interface IAcademicPeriodsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
        => operation(cancellationToken);
}
