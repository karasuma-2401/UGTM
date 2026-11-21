using Npgsql;

namespace UGTM.BuildingBlocks.Infrastructure.Caching;

public interface ICacheRevisionProvider
{
    Task<string> GetAsync(CancellationToken cancellationToken);
    void Reset();
}

/// <summary>
/// Captures one committed database revision per DI scope. Writers change the revision
/// in their own transaction, so a Redis outage cannot lose an invalidation.
/// </summary>
public sealed class PostgresCacheRevisionProvider(string connectionString) : ICacheRevisionProvider
{
    private readonly object _gate = new();
    private Task<string>? _snapshot;

    public Task<string> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return _snapshot ??= ReadAsync(cancellationToken);
        }
    }

    public void Reset()
    {
        lock (_gate) _snapshot = null;
    }

    private async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT revision::text FROM public.cache_revision WHERE singleton = true", connection)
        {
            CommandTimeout = 5
        };
        return await command.ExecuteScalarAsync(cancellationToken) as string
            ?? throw new InvalidOperationException("The cache revision is missing. Apply the cache revision migration.");
    }
}
