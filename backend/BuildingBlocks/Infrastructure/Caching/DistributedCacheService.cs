using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Application.Caching;

namespace UGTM.BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Triển khai <see cref="ICacheService"/> dựa trên <see cref="IDistributedCache"/> (Redis hoặc In-Memory fallback).
/// Thiết kế theo nguyên tắc Fail-Open: mọi lỗi kết nối Redis đều được ghi nhận log cảnh báo và bỏ qua
/// để API luôn đọc/ghi trực tiếp từ Database bình thường, không làm gián đoạn hệ thống.
/// </summary>
public sealed class DistributedCacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly TimeSpan _defaultTtl;
    private readonly TimeSpan _versionTtl;

    public DistributedCacheService(
        IDistributedCache cache,
        ILogger<DistributedCacheService>? logger = null,
        IOptions<RedisCacheOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        _logger = logger ?? NullLogger<DistributedCacheService>.Instance;

        var opts = options?.Value ?? new RedisCacheOptions();
        _defaultTtl = TimeSpan.FromMinutes(Math.Clamp(opts.DefaultTtlMinutes, 1, 1440));
        _versionTtl = TimeSpan.FromHours(Math.Clamp(opts.VersionTtlHours, 1, 720));
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return default;
        }

        try
        {
            var payload = await _cache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(payload))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(payload, SerializerOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DistributedCache GET failed for key '{CacheKey}'. Falling back to database.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || value is null)
        {
            return;
        }

        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            var entryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? _defaultTtl
            };

            await _cache.SetStringAsync(key, payload, entryOptions, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DistributedCache SET failed for key '{CacheKey}'. Continuing without caching.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DistributedCache REMOVE failed for key '{CacheKey}'.", key);
        }
    }

    public async Task<string> GetVersionAsync(string scopeKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            return "v0";
        }

        var versionKey = BuildVersionKey(scopeKey);
        try
        {
            var version = await _cache.GetStringAsync(versionKey, cancellationToken);
            return string.IsNullOrWhiteSpace(version) ? "v0" : version;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "DistributedCache GET VERSION failed for scope '{ScopeKey}'. Returning transient version to bypass stale cache.",
                scopeKey);
            return Guid.NewGuid().ToString("N");
        }
    }

    public async Task BumpVersionAsync(string scopeKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            return;
        }

        var versionKey = BuildVersionKey(scopeKey);
        try
        {
            var newVersion = Guid.NewGuid().ToString("N");
            var entryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _versionTtl
            };

            await _cache.SetStringAsync(versionKey, newVersion, entryOptions, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DistributedCache BUMP VERSION failed for scope '{ScopeKey}'.", scopeKey);
        }
    }

    private static string BuildVersionKey(string scopeKey) => $"ver:{scopeKey.Trim()}";
}
