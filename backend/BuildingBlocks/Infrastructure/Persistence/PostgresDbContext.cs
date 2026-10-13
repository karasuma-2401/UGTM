namespace UGTM.BuildingBlocks.Infrastructure.Persistence;

public abstract class PostgresDbContext(DbContextOptions options) : DbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50);
    }
}
