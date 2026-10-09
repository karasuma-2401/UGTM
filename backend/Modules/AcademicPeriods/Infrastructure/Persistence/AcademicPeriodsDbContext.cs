using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.AcademicPeriods.Domain.Entities;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;

public sealed class AcademicPeriodsDbContext(DbContextOptions<AcademicPeriodsDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => "academic";

    public DbSet<KltnBatch> KltnBatchs => Set<KltnBatch>();
    public DbSet<ScoreComponentConfig> ScoreComponentConfigs => Set<ScoreComponentConfig>();
    public DbSet<ScoreWeightConfig> ScoreWeightConfigs => Set<ScoreWeightConfig>();
    public DbSet<Stage> Stages => Set<Stage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AcademicPeriodsDbContext).Assembly);
    }
}
