using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Infrastructure.Persistence;

namespace UGTM.BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Đăng ký lớp caching phân tán (Redis hoặc tự động fallback sang In-Memory Cache khi chạy local/test).
/// </summary>
public static class RedisCacheRegistration
{
    public static IServiceCollection AddUgtmCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions();
        services.AddLogging();
        services.AddOptions<RedisCacheOptions>()
            .Bind(configuration.GetSection(RedisCacheOptions.SectionName))
            .Validate(options => options.DefaultTtlMinutes > 0 && options.MaxEntryTtlMinutes >= options.DefaultTtlMinutes
                && options.MaxEntryTtlMinutes <= 1440 && options.VersionTtlHours is >= 1 and <= 720
                && options.FailureBackoffSeconds is >= 1 and <= 60, "Invalid Redis cache TTL or failure backoff.")
            .ValidateOnStart();
        services.TryAddSingleton<CacheAvailability>();

        var cacheOptions = configuration.GetSection(RedisCacheOptions.SectionName).Get<RedisCacheOptions>() ?? new();
        if (cacheOptions.UseDatabaseRevisions
            && (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection"))
                || !string.IsNullOrWhiteSpace(configuration["DATABASE_CONNECTION_STRING"])))
        {
            var databaseConnection = DatabaseOptions.ResolveConnectionString(configuration);
            services.TryAddScoped<ICacheRevisionProvider>(_ => new PostgresCacheRevisionProvider(databaseConnection));
        }

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IDistributedCache)))
        {
            var redisConnection = RedisCacheOptions.ResolveConnectionString(configuration);
            if (!string.IsNullOrWhiteSpace(redisConnection))
            {
                var instanceName = configuration["Redis:InstanceName"];
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnection;
                    options.InstanceName = string.IsNullOrWhiteSpace(instanceName)
                        ? "ugtm:"
                        : instanceName.Trim();
                });
            }
            else
            {
                services.AddDistributedMemoryCache();
            }
        }

        services.TryAddScoped<ICacheService, DistributedCacheService>();
        return services;
    }
}
