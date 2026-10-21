using Npgsql;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.AcademicPeriods.Application.Persistence;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;

public sealed class AcademicPeriodsDbContext(DbContextOptions<AcademicPeriodsDbContext> options)
    : ModuleDbContext(options), IAcademicPeriodsUnitOfWork
{
    public override string Schema => "academic";

    public DbSet<AuditOutboxMessage> AuditOutboxMessages => Set<AuditOutboxMessage>();
    public DbSet<KltnBatch> KltnBatches => Set<KltnBatch>();
    public DbSet<ScheduleTemplateVersion> ScheduleTemplateVersions => Set<ScheduleTemplateVersion>();
    public DbSet<StageTemplateDefinition> StageTemplateDefinitions => Set<StageTemplateDefinition>();
    public DbSet<ScoreComponentConfig> ScoreComponentConfigs => Set<ScoreComponentConfig>();
    public DbSet<ScoreWeightConfig> ScoreWeightConfigs => Set<ScoreWeightConfig>();
    public DbSet<Stage> Stages => Set<Stage>();

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new AcademicPeriodsConcurrencyException(ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            } postgresException)
        {
            throw new AcademicPeriodsUniqueConstraintException(postgresException.ConstraintName, ex);
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AcademicPeriodsDbContext).Assembly);
    }
}
