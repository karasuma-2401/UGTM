using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UGTM.Persistence;

public class UGTMDbContextFactory : IDesignTimeDbContextFactory<UGTMDbContext>
{
    public UGTMDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UGTMDbContext>();

        // Fallback dummy connection string for EF Core design-time operations (migrations add, script, etc.)
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? "Host=localhost;Database=ugtm_design;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__ef_migrations_history", Schemas.Shared))
            .UseSnakeCaseNamingConvention();

        return new UGTMDbContext(optionsBuilder.Options);
    }
}
