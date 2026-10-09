using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Infrastructure.Persistence;

namespace UGTM.Database.Migrations;

public sealed class UGTMDbContext(DbContextOptions<UGTMDbContext> options) : PostgresDbContext(options)
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("UGTMDbContext is for migrations only. Use the owning module's DbContext for application writes.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("UGTMDbContext is for migrations only. Use the owning module's DbContext for application writes.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AcademicPeriodsDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TopicCatalogDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThesisManagementDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DefenseCouncilDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        CrossModuleForeignKeys.Configure(modelBuilder);
    }
}
