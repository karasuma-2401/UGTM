using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace UGTM.BuildingBlocks.Infrastructure.Persistence;

public static class DatabaseOptions
{
    public static string ResolveConnectionString(IConfiguration configuration, bool forMigrations = false)
    {
        string?[] candidates = forMigrations
            ? [configuration.GetConnectionString("MigrationConnection"), configuration["MIGRATION_CONNECTION_STRING"],
                configuration.GetConnectionString("DefaultConnection"), configuration["DATABASE_CONNECTION_STRING"]]
            : [configuration.GetConnectionString("DefaultConnection"), configuration["DATABASE_CONNECTION_STRING"]];

        var connectionString = candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:DefaultConnection or DATABASE_CONNECTION_STRING. " +
                "For Neon migrations, MIGRATION_CONNECTION_STRING may specify a direct connection.");
        }

        NpgsqlConnectionStringBuilder settings;
        try
        {
            settings = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("The PostgreSQL connection string is invalid. Use Npgsql key/value format.");
        }

        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Database))
        {
            throw new InvalidOperationException("The PostgreSQL connection string must specify Host and Database.");
        }

        return settings.ConnectionString;
    }

    public static void Configure(DbContextOptionsBuilder options, string connectionString, string? migrationAssembly = null)
    {
        options.UseNpgsql(connectionString, npgsql =>
            {
                if (migrationAssembly is not null)
                {
                    npgsql.MigrationsAssembly(migrationAssembly);
                }
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "public");
                npgsql.CommandTimeout(60);
            })
            .UseSnakeCaseNamingConvention();
    }
}
