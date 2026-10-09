using UGTM.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace UGTM.Database.Migrations;

public class UGTMDbContextFactory : IDesignTimeDbContextFactory<UGTMDbContext>
{
    public UGTMDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UGTMDbContext>();

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var connectionString = DatabaseOptions.ResolveConnectionString(configuration, forMigrations: true);
        DatabaseOptions.Configure(optionsBuilder, connectionString, typeof(UGTMDbContext).Assembly.GetName().Name);

        return new UGTMDbContext(optionsBuilder.Options);
    }
}
