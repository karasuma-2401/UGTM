using Microsoft.Extensions.Configuration;

namespace UGTM.BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Tùy chọn cấu hình cho hệ thống Redis Distributed Cache trong UGTM.
/// Tương thích với biến môi trường Docker Compose (<c>Redis__ConnectionString</c>).
/// </summary>
public sealed class RedisCacheOptions
{
    public const string SectionName = "Redis";

    public string? ConnectionString { get; set; }

    public string InstanceName { get; set; } = "ugtm:";

    public int DefaultTtlMinutes { get; set; } = 5;

    public int VersionTtlHours { get; set; } = 24;

    public static string? ResolveConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string?[] candidates =
        [
            configuration["Redis:ConnectionString"],
            configuration.GetConnectionString("Redis"),
            configuration["REDIS_CONNECTION_STRING"]
        ];

        var raw = candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return string.IsNullOrWhiteSpace(raw) ? null : NormalizeConnectionString(raw);
    }

    public static string NormalizeConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var normalized = connectionString.Trim().TrimEnd(',');
        if (!normalized.Contains("abortConnect=", StringComparison.OrdinalIgnoreCase))
        {
            normalized += ",abortConnect=false";
        }

        if (!normalized.Contains("connectTimeout=", StringComparison.OrdinalIgnoreCase))
        {
            normalized += ",connectTimeout=2000";
        }

        if (!normalized.Contains("syncTimeout=", StringComparison.OrdinalIgnoreCase))
        {
            normalized += ",syncTimeout=2000";
        }

        return normalized;
    }
}
