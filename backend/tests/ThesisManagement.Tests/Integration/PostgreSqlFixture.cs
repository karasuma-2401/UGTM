using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private const string TemplateDatabase = "ugtm_migrated_template";
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();
    private bool _templateCreated;
    private string? _databaseName;
    private int _databaseNumber;
    private TimeSpan _migrationTime;
    private TimeSpan _cloneTime;

    public string ConnectionString => _databaseName is null
        ? _container.GetConnectionString()
        : new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = _databaseName }.ConnectionString;

    public Task InitializeAsync() => _container.StartAsync();

    // Call before creating contexts/hosts. The PostgreSQL collection serializes callers.
    // A fresh clone preserves migration seeds, sequences, functions and triggers, even
    // when the preceding test changed the schema or rolled migrations back.
    // It also owns database cleanup: the next reset drops the previous clone,
    // and container disposal removes the last clone and the template.
    public async Task ResetToMigratedAsync()
    {
        await using var admin = new NpgsqlConnection(_container.GetConnectionString());
        await admin.OpenAsync();

        if (!_templateCreated)
        {
            await using (var create = new NpgsqlCommand($"CREATE DATABASE {TemplateDatabase} TEMPLATE template0", admin))
            {
                await create.ExecuteNonQueryAsync();
            }

            var settings = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
            {
                Database = TemplateDatabase,
                Pooling = false
            };
            var options = new DbContextOptionsBuilder<UGTMDbContext>();
            DatabaseOptions.Configure(options, settings.ConnectionString, typeof(UGTMDbContext).Assembly.GetName().Name);
            await using (var db = new UGTMDbContext(options.Options))
            {
                var migrationStarted = Stopwatch.GetTimestamp();
                await db.Database.MigrateAsync();
                _migrationTime = Stopwatch.GetElapsedTime(migrationStarted);
            }

            await using var protect = new NpgsqlCommand($"ALTER DATABASE {TemplateDatabase} ALLOW_CONNECTIONS false", admin);
            await protect.ExecuteNonQueryAsync();
            _templateCreated = true;
        }

        var cloneStarted = Stopwatch.GetTimestamp();
        if (_databaseName is not null)
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {_databaseName} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }

        // Names are generated here, never supplied externally. Changing the name
        // also keeps EF/Npgsql pools from reusing connections to the discarded DB.
        var databaseName = $"ugtm_test_{++_databaseNumber}";
        await using var clone = new NpgsqlCommand($"CREATE DATABASE {databaseName} TEMPLATE {TemplateDatabase}", admin);
        await clone.ExecuteNonQueryAsync();
        _databaseName = databaseName;
        _cloneTime += Stopwatch.GetElapsedTime(cloneStarted);
    }

    public async Task DisposeAsync()
    {
        Console.WriteLine($"PostgreSQL template setup: migration {_migrationTime.TotalSeconds:F2}s, " +
            $"{_databaseNumber} clones {_cloneTime.TotalSeconds:F2}s total.");
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "ThesisManagement PostgreSQL integration";
}
