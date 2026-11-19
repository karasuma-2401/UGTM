using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using UGTM.BuildingBlocks.Infrastructure.Caching;

namespace UGTM.Api.Campus;

public sealed record CampusHeartbeat(Guid VisitorId, Guid TabId, bool Leaving = false);
public sealed record CampusSnapshot(string[] Visitors, long ServerTime);

public interface ICampusPresenceStore
{
    Task<CampusSnapshot> HeartbeatAsync(CampusHeartbeat heartbeat, string? userId);
}

public static class CampusPresence
{
    public const int LeaseSeconds = 75;
    public const int Capacity = 5000;

    public static IServiceCollection AddCampusPresence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        var connection = RedisCacheOptions.ResolveConnectionString(configuration);
        if (connection is null)
            services.AddSingleton<ICampusPresenceStore, MemoryCampusPresenceStore>();
        else
            services.AddSingleton<ICampusPresenceStore>(sp => new RedisCampusPresenceStore(
                connection, configuration["Redis:InstanceName"] ?? "ugtm:",
                sp.GetRequiredService<ILogger<RedisCampusPresenceStore>>()));
        services.AddRateLimiter(options => options.AddPolicy("campus-presence", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 600,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                })));
        return services;
    }

    public static void MapCampusPresence(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/campus/presence", async (
            CampusHeartbeat heartbeat, HttpContext context, ICampusPresenceStore store) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (heartbeat.VisitorId == Guid.Empty || heartbeat.TabId == Guid.Empty)
                return Results.BadRequest();
            if (context.Request.Headers.ContainsKey("Authorization") && context.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();
            var userId = context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;
            try
            {
                return Results.Ok(await store.HeartbeatAsync(heartbeat, userId));
            }
            catch (RedisException)
            {
                // Never turn a shared-store outage into a misleading, isolated local count.
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (InvalidOperationException)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }).AllowAnonymous().RequireRateLimiting("campus-presence").WithTags("Campus");
    }

    internal static string Actor(Guid visitorId, string? userId) => userId is null
        ? $"g{visitorId:N}"
        : "u" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("campus:" + userId)));

    internal static string[] VisibleActors(IEnumerable<string> values) => values
        .Select(value => value.Split('|', 2))
        .GroupBy(value => value[0])
        // A guest tab and a signed-in tab on the same browser represent one person.
        .Select(group => group.Select(value => value[1]).OrderByDescending(actor => actor[0]).First())
        .Distinct().Order(StringComparer.Ordinal).ToArray();
}

public sealed class MemoryCampusPresenceStore(TimeProvider time) : ICampusPresenceStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (string Value, long Expires)> _leases = [];

    public Task<CampusSnapshot> HeartbeatAsync(CampusHeartbeat heartbeat, string? userId)
    {
        lock (_gate)
        {
            var now = time.GetUtcNow().ToUnixTimeMilliseconds();
            foreach (var key in _leases.Where(entry => entry.Value.Expires <= now).Select(entry => entry.Key).ToArray())
                _leases.Remove(key);
            var keyName = $"{heartbeat.VisitorId:N}:{heartbeat.TabId:N}";
            if (heartbeat.Leaving) _leases.Remove(keyName);
            else
            {
                if (_leases.Count >= CampusPresence.Capacity && !_leases.ContainsKey(keyName))
                    throw new InvalidOperationException("Campus presence capacity reached.");
                _leases[keyName] = ($"{heartbeat.VisitorId:N}|{CampusPresence.Actor(heartbeat.VisitorId, userId)}",
                    now + CampusPresence.LeaseSeconds * 1000);
            }
            return Task.FromResult(new CampusSnapshot(CampusPresence.VisibleActors(_leases.Values.Select(entry => entry.Value)), now));
        }
    }
}

public sealed class RedisCampusPresenceStore : ICampusPresenceStore, IDisposable
{
    // Both keys share a Redis Cluster hash slot. Expiry, removal and heartbeat are atomic.
    private const string Script = """
        local t = redis.call('TIME')
        local now = t[1] * 1000 + math.floor(t[2] / 1000)
        local expired = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', now)
        for _, key in ipairs(expired) do
            redis.call('HDEL', KEYS[2], key)
        end
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', now)
        if ARGV[3] == 'leave' then
            redis.call('ZREM', KEYS[1], ARGV[1])
            redis.call('HDEL', KEYS[2], ARGV[1])
        else
            if redis.call('ZCARD', KEYS[1]) >= tonumber(ARGV[5]) and not redis.call('ZSCORE', KEYS[1], ARGV[1]) then
                return redis.error_reply('Campus presence capacity reached')
            end
            redis.call('ZADD', KEYS[1], now + tonumber(ARGV[4]) * 1000, ARGV[1])
            redis.call('HSET', KEYS[2], ARGV[1], ARGV[2])
        end
        redis.call('EXPIRE', KEYS[1], ARGV[4])
        redis.call('EXPIRE', KEYS[2], ARGV[4])
        return {now, redis.call('HVALS', KEYS[2])}
        """;
    private readonly Lazy<ConnectionMultiplexer> _connection;
    private readonly RedisKey[] _keys;
    private readonly ILogger<RedisCampusPresenceStore> _logger;

    public RedisCampusPresenceStore(string connection, string prefix, ILogger<RedisCampusPresenceStore> logger)
    {
        _connection = new(() => ConnectionMultiplexer.Connect(connection));
        _keys = [$"{prefix}{{campus-presence}}:leases", $"{prefix}{{campus-presence}}:actors"];
        _logger = logger;
    }

    public async Task<CampusSnapshot> HeartbeatAsync(CampusHeartbeat heartbeat, string? userId)
    {
        try
        {
            var result = (RedisResult[])(await _connection.Value.GetDatabase().ScriptEvaluateAsync(Script, _keys,
                [$"{heartbeat.VisitorId:N}:{heartbeat.TabId:N}",
                 $"{heartbeat.VisitorId:N}|{CampusPresence.Actor(heartbeat.VisitorId, userId)}",
                 heartbeat.Leaving ? "leave" : "join", CampusPresence.LeaseSeconds, CampusPresence.Capacity]))!;
            return new(CampusPresence.VisibleActors((string[])result[1]!), (long)result[0]);
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Campus presence shared store unavailable.");
            throw;
        }
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated) _connection.Value.Dispose();
    }
}
