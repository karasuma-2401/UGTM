using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Domain.Entities;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

public sealed class TopicCatalogDbContext(DbContextOptions<TopicCatalogDbContext> options) : ModuleDbContext(options), ITopicUnitOfWork
{
    public override string Schema => "topics";

    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<NotificationEventOutboxMessage> NotificationEventOutboxMessages => Set<NotificationEventOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TopicCatalogDbContext).Assembly);
    }
}
