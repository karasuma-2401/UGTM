using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Application.Persistence;

namespace UGTM.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityUnitOfWork(IdentityDbContext dbContext)
    : IIdentityUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
            return await operation(cancellationToken);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(cancellationToken);
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
