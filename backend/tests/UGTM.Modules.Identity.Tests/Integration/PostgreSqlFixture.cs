using Testcontainers.PostgreSql;
using Npgsql;

namespace UGTM.Modules.Identity.Tests.Integration;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly string? _localConnection = GetLocalTestConnection();
    private readonly PostgreSqlContainer? _container;

    public PostgreSqlFixture()
    {
        if (_localConnection is null) _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    }

    public string ConnectionString => _localConnection ?? _container!.GetConnectionString();

    public Task InitializeAsync() => _localConnection is null ? _container!.StartAsync() : Task.CompletedTask;

    public Task DisposeAsync() => _localConnection is null ? _container!.DisposeAsync().AsTask() : Task.CompletedTask;

    private static string? GetLocalTestConnection()
    {
        var value = Environment.GetEnvironmentVariable("UGTM_AUDIT_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(value)) return null;
        var settings = new NpgsqlConnectionStringBuilder(value);
        if (settings.Host is not ("127.0.0.1" or "localhost") || settings.Database != "ugtm_audit_test")
            throw new InvalidOperationException("The local test database must be ugtm_audit_test on localhost.");
        return value;
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "Identity PostgreSQL integration";
}
