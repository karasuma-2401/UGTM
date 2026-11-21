namespace UGTM.BuildingBlocks.Application.Caching;

/// <summary>
/// Cổng giao tiếp trừu tượng cho lớp Distributed Cache dùng chung giữa các module.
/// Hỗ trợ Cache-Aside và Versioned Cache Keys. Production dùng revision database chung;
/// service được giữ trong scope của một request/job để chụp revision trước khi nạp dữ liệu.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task<string> GetVersionAsync(string scopeKey, CancellationToken cancellationToken = default);

    Task BumpVersionAsync(string scopeKey, CancellationToken cancellationToken = default);

    async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        TimeSpan expiration, CancellationToken cancellationToken = default) where T : class
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null) return cached;
        var value = await factory(cancellationToken);
        await SetAsync(key, value, expiration, cancellationToken);
        return value;
    }
}
