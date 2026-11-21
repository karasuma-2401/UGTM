using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using DotNet.Testcontainers.Builders;
using Npgsql;
using UGTM.BuildingBlocks.Infrastructure.Caching;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class CacheRevisionIntegrationTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetToMigratedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Revision_changes_only_with_committed_source_changes_and_invalidates_another_api_scope()
    {
        var store = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var originalProvider = new PostgresCacheRevisionProvider(fixture.ConnectionString);
        var originalApi = new DistributedCacheService(store, revisions: originalProvider);
        var original = await originalApi.GetVersionAsync("global");
        await originalApi.SetAsync($"results:{original}", "old-results");

        await using var writer = new NpgsqlConnection(fixture.ConnectionString);
        await writer.OpenAsync();
        await using var transaction = await writer.BeginTransactionAsync();
        await using var mutation = new NpgsqlCommand("UPDATE identity.role SET role_name = role_name WHERE role_name = 'Student'", writer, transaction);
        Assert.Equal(1, await mutation.ExecuteNonQueryAsync());
        Assert.Equal(original, await new PostgresCacheRevisionProvider(fixture.ConnectionString).GetAsync(default));
        await transaction.CommitAsync();

        var nextApi = new DistributedCacheService(store, revisions: new PostgresCacheRevisionProvider(fixture.ConnectionString));
        var committed = await nextApi.GetVersionAsync("global");
        Assert.NotEqual(original, committed);
        Assert.Null(await nextApi.GetAsync<string>($"results:{committed}"));
        Assert.Equal(original, await originalApi.GetVersionAsync("global"));
        originalProvider.Reset();
        Assert.Equal(committed, await originalApi.GetVersionAsync("global"));
    }

    [Fact]
    public async Task Rolled_back_mutation_does_not_change_the_revision()
    {
        var original = await RevisionAsync();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("UPDATE identity.role SET role_name = role_name WHERE role_name = 'Student'", connection, transaction);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
        await transaction.RollbackAsync();
        Assert.Equal(original, await RevisionAsync());
    }

    [Fact]
    public async Task Changes_in_other_modules_and_rubric_usage_are_covered_by_deferred_revision_triggers()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT n.nspname || '.' || c.relname
            FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE t.tgname = 'cache_revision_changed' AND t.tgdeferrable AND t.tginitdeferred
            """, connection);
        var covered = new HashSet<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) covered.Add(reader.GetString(0));
        Assert.Contains("topics.topic", covered);
        Assert.Contains("identity.user", covered);
        Assert.Contains("identity.user_role", covered);
        Assert.Contains("academic.score_weight_config", covered);
        Assert.Contains("academic.score_component_config", covered);
        Assert.Contains("thesis.midterm_report", covered);
        Assert.Contains("thesis.submission_review", covered);
        Assert.Contains("thesis.final_submission", covered);
        Assert.Contains("thesis.final_submission_review", covered);
        Assert.Contains("councils.reviewer_assignment", covered);
        Assert.Contains("councils.grading_rubric_usage", covered);
        Assert.Contains("councils.evaluation_detail", covered);
        Assert.DoesNotContain("identity.audit_log", covered);
    }

    private Task<string> RevisionAsync() => new PostgresCacheRevisionProvider(fixture.ConnectionString).GetAsync(default);

    [Fact]
    public async Task Redis_restart_after_a_database_commit_cannot_reuse_the_persisted_old_score()
    {
        const string password = "redis-restart-test-only";
        await using var redis = new ContainerBuilder("redis:7-alpine")
            .WithCommand("redis-server", "--requirepass", password, "--appendonly", "yes", "--save", "")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("redis-cli", "-a", password, "ping"))
            .Build();
        await redis.StartAsync();
        var prefix = $"revision-test:{Guid.NewGuid():N}:";
        Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache RedisStore() => new(Options.Create(
            new Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions
            {
                Configuration = $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)},password={password},abortConnect=false,connectTimeout=2000,syncTimeout=2000",
                InstanceName = prefix
            }));
        using var originalStore = RedisStore();
        var originalApi = new DistributedCacheService(originalStore, revisions: new PostgresCacheRevisionProvider(fixture.ConnectionString));
        var originalVersion = await originalApi.GetVersionAsync("global");
        await originalApi.SetAsync($"results:{originalVersion}", "old-score");
        await redis.StopAsync();

        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("UPDATE identity.role SET role_name = role_name WHERE role_name = 'Student'", connection);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
        await originalApi.BumpVersionAsync("global");
        await redis.StartAsync();

        using var recoveredStore = RedisStore();
        var nextApi = new DistributedCacheService(recoveredStore, revisions: new PostgresCacheRevisionProvider(fixture.ConnectionString));
        Assert.Equal("old-score", await nextApi.GetAsync<string>($"results:{originalVersion}"));
        var committedVersion = await nextApi.GetVersionAsync("global");
        Assert.NotEqual(originalVersion, committedVersion);
        Assert.Null(await nextApi.GetAsync<string>($"results:{committedVersion}"));
    }

    [Fact]
    public async Task Revision_migration_can_be_reversed_and_reapplied_without_business_data_loss()
    {
        var before = await RevisionAsync();
        var options = new DbContextOptionsBuilder<UGTMDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString, typeof(UGTMDbContext).Assembly.GetName().Name);
        await using var migration = new UGTMDbContext(options.Options);
        var originalUsers = await migration.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM identity.\"user\"").SingleAsync();
        await migration.Database.GetService<IMigrator>().MigrateAsync("20261120113641_AddUserMustChangePassword");
        Assert.Equal(0, await migration.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_tables WHERE schemaname = 'public' AND tablename = 'cache_revision'").SingleAsync());
        await migration.Database.MigrateAsync();
        Assert.NotEqual(before, await RevisionAsync());
        Assert.Equal(originalUsers, await migration.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM identity.\"user\"").SingleAsync());
        Assert.False(migration.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Concurrent_writers_commit_without_losing_a_revision()
    {
        var original = await RevisionAsync();
        await using var first = new NpgsqlConnection(fixture.ConnectionString);
        await using var second = new NpgsqlConnection(fixture.ConnectionString);
        await first.OpenAsync();
        await second.OpenAsync();
        await using var firstTransaction = await first.BeginTransactionAsync();
        await using var secondTransaction = await second.BeginTransactionAsync();
        await using var firstCommand = new NpgsqlCommand("UPDATE identity.role SET role_name = role_name WHERE role_name = 'Student'", first, firstTransaction);
        await using var secondCommand = new NpgsqlCommand("UPDATE identity.role SET role_name = role_name WHERE role_name = 'Lecturer'", second, secondTransaction);
        Assert.Equal(1, await firstCommand.ExecuteNonQueryAsync());
        Assert.Equal(1, await secondCommand.ExecuteNonQueryAsync());
        await firstTransaction.CommitAsync();
        var afterFirst = await RevisionAsync();
        await secondTransaction.CommitAsync();
        Assert.NotEqual(original, afterFirst);
        Assert.NotEqual(afterFirst, await RevisionAsync());
    }
}
