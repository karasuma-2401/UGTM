namespace UGTM.BuildingBlocks.Application.Caching;

/// <summary>
/// Cổng giao tiếp trừu tượng cho lớp Distributed Cache dùng chung giữa các module.
/// Hỗ trợ cơ chế Cache-Aside và Versioned Cache Keys để vô hiệu hóa hàng loạt khóa con theo phạm vi.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task<string> GetVersionAsync(string scopeKey, CancellationToken cancellationToken = default);

    Task BumpVersionAsync(string scopeKey, CancellationToken cancellationToken = default);
}
