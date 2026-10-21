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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AcademicPeriodsDbContext).Assembly);
    }
}
