using Microsoft.EntityFrameworkCore;

namespace UGTM.BuildingBlocks.Infrastructure.Persistence;

public abstract class ModuleDbContext(DbContextOptions options) : PostgresDbContext(options)
{
    public abstract string Schema { get; }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateWriteOwnership();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateWriteOwnership();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateWriteOwnership()
    {
        foreach (var entry in ChangeTracker.Entries().Where(entry =>
                     entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Metadata.GetSchema() != Schema)
            {
                throw new InvalidOperationException($"{GetType().Name} cannot write outside its '{Schema}' schema.");
            }
        }
    }
}
