using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Npgsql;
using UGTM.Database.Migrations;
using UGTM.BuildingBlocks.Infrastructure.Persistence;

namespace UGTM.Architecture.Tests;

public class DatabaseConfigurationTests
{
    private const string RuntimeConnection = "Host=localhost;Database=runtime_test;Username=test";
    private const string MigrationConnection = "Host=localhost;Database=migration_test;Username=test";

    [Fact]
    public void Runtime_ignores_migration_connection_and_migrations_prefer_it()
    {
        var config = Configuration(new()
        {
            ["DATABASE_CONNECTION_STRING"] = RuntimeConnection,
            ["MIGRATION_CONNECTION_STRING"] = MigrationConnection
        });

        Assert.Equal("runtime_test", DatabaseName(DatabaseOptions.ResolveConnectionString(config)));
        Assert.Equal("migration_test", DatabaseName(DatabaseOptions.ResolveConnectionString(config, true)));
    }

    [Fact]
    public void Configuration_connection_overrides_environment_fallback()
    {
        var config = Configuration(new()
        {
            ["ConnectionStrings:DefaultConnection"] = RuntimeConnection,
            ["DATABASE_CONNECTION_STRING"] = MigrationConnection
        });

        Assert.Equal("runtime_test", DatabaseName(DatabaseOptions.ResolveConnectionString(config, true)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Host=localhost")]
    [InlineData("not-a-connection-string")]
    public void Missing_or_invalid_database_configuration_fails_before_connection(string? value)
    {
        var config = Configuration(new() { ["DATABASE_CONNECTION_STRING"] = value });
        Assert.Throws<InvalidOperationException>(() => DatabaseOptions.ResolveConnectionString(config));
    }

    [Fact]
    public void Common_options_keep_Npgsql_and_technical_migration_history_in_public_schema()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(options, RuntimeConnection, "UGTM.Database.Migrations");
        using var context = new UGTMDbContext(options.Options);
        var relationalOptions = options.Options.Extensions.OfType<RelationalOptionsExtension>().Single();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal("public", relationalOptions.MigrationsHistoryTableSchema);
        Assert.Equal("__ef_migrations_history", relationalOptions.MigrationsHistoryTableName);
        Assert.Equal("UGTM.Database.Migrations", relationalOptions.MigrationsAssembly);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static string? DatabaseName(string connection) => new NpgsqlConnectionStringBuilder(connection).Database;

    [Fact]
    public void Business_entities_are_mapped_to_their_own_data_area_without_a_shared_schema()
    {
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(options, RuntimeConnection);
        using var context = new UGTMDbContext(options.Options);
        var areas = new Dictionary<string, string>
        {
            ["Identity"] = "identity",
            ["AcademicPeriods"] = "academic",
            ["TopicCatalog"] = "topics",
            ["ThesisManagement"] = "thesis",
            ["DefenseCouncil"] = "councils",
            ["Notifications"] = "notifications"
        };

        Assert.All(context.Model.GetEntityTypes(), entity =>
        {
            var area = entity.ClrType.Namespace!.Split('.')[2];
            Assert.True(areas.TryGetValue(area, out var schema), $"Unknown data owner: {area}");
            Assert.Equal(schema, entity.GetSchema());
        });
    }
}
