using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UGTM.BuildingBlocks.Application.Caching;

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
        services.Configure<RedisCacheOptions>(configuration.GetSection(RedisCacheOptions.SectionName));

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

        services.TryAddSingleton<ICacheService, DistributedCacheService>();
        return services;
    }
}
