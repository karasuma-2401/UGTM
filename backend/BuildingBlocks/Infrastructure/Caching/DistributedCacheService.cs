using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Application.Caching;

namespace UGTM.BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Cache-aside with committed database revisions and a shared outage backoff.
/// Database revisions, rather than best-effort Redis writes, protect production reads.
/// </summary>
public sealed class DistributedCacheService : ICacheService
{
    public const string DataKeyPrefix = "cache:v2:";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly ICacheRevisionProvider? _revisions;
    private readonly CacheAvailability _availability;
    private readonly TimeSpan _defaultTtl;
    private readonly TimeSpan _maximumTtl;
    private readonly TimeSpan _versionTtl;
    private readonly TimeSpan _failureBackoff;
    private bool _revisionFailed;

    public DistributedCacheService(
        IDistributedCache cache,
        ILogger<DistributedCacheService>? logger = null,
        IOptions<RedisCacheOptions>? options = null,
        ICacheRevisionProvider? revisions = null,
        CacheAvailability? availability = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        _logger = logger ?? NullLogger<DistributedCacheService>.Instance;
        _revisions = revisions;
        _availability = availability ?? new CacheAvailability();
        var opts = options?.Value ?? new RedisCacheOptions();
        _defaultTtl = TimeSpan.FromMinutes(Math.Clamp(opts.DefaultTtlMinutes, 1, 1440));
        _maximumTtl = TimeSpan.FromMinutes(Math.Clamp(opts.MaxEntryTtlMinutes, 1, 1440));
        _versionTtl = TimeSpan.FromHours(Math.Clamp(opts.VersionTtlHours, 1, 720));
        _failureBackoff = TimeSpan.FromSeconds(Math.Clamp(opts.FailureBackoffSeconds, 1, 60));
    }

    private bool CanUseCache => !_revisionFailed && _availability.IsAvailable;

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        TimeSpan expiration, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null) return cached;
        if (!CanUseCache) return await factory(cancellationToken);

        // Bounded locks coalesce fills within this API process without retaining arbitrary keys.
        var gate = _availability.FillLock(key);
        await gate.WaitAsync(cancellationToken);
        try
        {
            cached = await GetAsync<T>(key, cancellationToken);
            if (cached is not null) return cached;
            var value = await factory(cancellationToken);
            await SetAsync(key, value, expiration, cancellationToken);
            return value;
        }
        finally { gate.Release(); }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(key)) return default;
        if (!CanUseCache)
        {
            CacheAvailability.Bypasses.Add(1);
            return default;
        }
        try
        {
            var payload = await _cache.GetStringAsync(DataKeyPrefix + key, cancellationToken);
            if (string.IsNullOrEmpty(payload))
            {
                CacheAvailability.Misses.Add(1);
                return default;
            }
            var value = JsonSerializer.Deserialize<T>(payload, SerializerOptions);
            CacheAvailability.Hits.Add(1);
            return value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            Failed(ex, "GET", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(key) || value is null || !CanUseCache) return;
        try
        {
            var ttl = expiration ?? _defaultTtl;
            if (ttl <= TimeSpan.Zero) return;
            if (ttl > _maximumTtl) ttl = _maximumTtl;
            // Only shorten TTLs: the configured maximum also bounds unsafe-cache backoff.
            ttl = TimeSpan.FromTicks(Math.Max(1L, (long)(ttl.Ticks * (0.9 + Random.Shared.NextDouble() * 0.1))));
            await _cache.SetStringAsync(DataKeyPrefix + key, JsonSerializer.Serialize(value, SerializerOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) { Failed(ex, "SET", key); }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) _availability.BackOff(_maximumTtl);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(key)) return;
        try { await _cache.RemoveAsync(DataKeyPrefix + key, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _availability.BackOff(_maximumTtl);
            throw;
        }
        catch (Exception ex)
        {
            _availability.BackOff(_maximumTtl);
            Failed(ex, "REMOVE", key);
        }
    }

    public async Task<string> GetVersionAsync(string scopeKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_revisions is not null)
        {
            try { return await _revisions.GetAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _revisionFailed = true;
                _logger.LogWarning(ex, "Database cache revision unavailable. Bypassing cache for this request.");
                CacheAvailability.Failures.Add(1);
                return Guid.NewGuid().ToString("N");
            }
        }
        if (string.IsNullOrWhiteSpace(scopeKey) || !CanUseCache) return Guid.NewGuid().ToString("N");

        // Local/test mode only. Production uses a transactional database revision.
        var versionKey = BuildVersionKey(scopeKey);
        await _availability.VersionLock.WaitAsync(cancellationToken);
        try
        {
            var version = await _cache.GetStringAsync(versionKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(version)) return version;
            version = Guid.NewGuid().ToString("N");
            await _cache.SetStringAsync(versionKey, version,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _versionTtl }, cancellationToken);
            return version;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            Failed(ex, "GET VERSION", scopeKey);
            return Guid.NewGuid().ToString("N");
        }
        finally { _availability.VersionLock.Release(); }
    }

    public async Task BumpVersionAsync(string scopeKey, CancellationToken cancellationToken = default)
    {
        if (_revisions is not null)
        {
            // Reset even after client cancellation: a committed mutation must remain visible.
            _revisions.Reset();
            return;
        }
        if (string.IsNullOrWhiteSpace(scopeKey)) return;
        if (cancellationToken.IsCancellationRequested || !CanUseCache)
        {
            _availability.BackOff(_maximumTtl);
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }
        await _availability.VersionLock.WaitAsync(cancellationToken);
        try
        {
            await _cache.SetStringAsync(BuildVersionKey(scopeKey), Guid.NewGuid().ToString("N"),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _versionTtl }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _availability.BackOff(_maximumTtl);
            throw;
        }
        catch (Exception ex)
        {
            _availability.BackOff(_maximumTtl);
            Failed(ex, "BUMP VERSION", scopeKey);
        }
        finally { _availability.VersionLock.Release(); }
    }

    private void Failed(Exception exception, string operation, string key)
    {
        _availability.BackOff(_failureBackoff);
        CacheAvailability.Failures.Add(1);
        _logger.LogWarning(exception, "Distributed cache {Operation} failed for '{CacheKey}'. Bypassing cache.", operation, key);
    }

    private static string BuildVersionKey(string scopeKey) => $"{DataKeyPrefix}ver:{scopeKey.Trim()}";
}
