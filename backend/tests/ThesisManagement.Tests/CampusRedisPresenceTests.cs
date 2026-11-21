using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using UGTM.Api.Campus;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class CampusRedisPresenceTests
{
    [Theory]
    [InlineData("leases")]
    [InlineData("actors")]
    public async Task Partial_key_eviction_discards_orphaned_presence_state(string evictedPart)
    {
        await using var redis = new ContainerBuilder("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
            .Build();
        await redis.StartAsync();
        var connection = $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)},abortConnect=false";
        var prefix = $"campus-eviction:{Guid.NewGuid():N}:";
        using var store = new RedisCampusPresenceStore(connection, prefix, NullLogger<RedisCampusPresenceStore>.Instance);
        var departed = new CampusHeartbeat(Guid.NewGuid(), Guid.NewGuid());
        var active = new CampusHeartbeat(Guid.NewGuid(), Guid.NewGuid());
        await store.HeartbeatAsync(departed, null);
        Assert.Equal(2, (await store.HeartbeatAsync(active, "active-user")).Visitors.Length);
        using var client = await ConnectionMultiplexer.ConnectAsync(connection);
        await client.GetDatabase().KeyDeleteAsync($"{prefix}{{campus-presence}}:{evictedPart}");

        var recovered = await store.HeartbeatAsync(active, "active-user");

        Assert.Single(recovered.Visitors);
        Assert.Equal(recovered.Visitors, (await store.HeartbeatAsync(active, "active-user")).Visitors);
    }

    [Fact]
    public async Task Separate_api_instances_share_redis_leases_and_account_deduplication()
    {
        await using var redis = new ContainerBuilder("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
            .Build();
        await redis.StartAsync();
        var connection = $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)},abortConnect=false";
        var prefix = $"campus-test:{Guid.NewGuid():N}:";
        using var first = new RedisCampusPresenceStore(connection, prefix, NullLogger<RedisCampusPresenceStore>.Instance);
        using var second = new RedisCampusPresenceStore(connection, prefix, NullLogger<RedisCampusPresenceStore>.Instance);
        var guest = new CampusHeartbeat(Guid.NewGuid(), Guid.NewGuid());
        var user = new CampusHeartbeat(Guid.NewGuid(), Guid.NewGuid());
        await first.HeartbeatAsync(guest, null);
        var shared = await second.HeartbeatAsync(user, "same-account");
        Assert.Equal(2, shared.Visitors.Length);
        Assert.Equal(shared.Visitors, (await first.HeartbeatAsync(guest, null)).Visitors);
        Assert.Equal(2, (await first.HeartbeatAsync(new(Guid.NewGuid(), Guid.NewGuid()), "same-account")).Visitors.Length);
        Assert.Single((await second.HeartbeatAsync(guest with { Leaving = true }, null)).Visitors);
        // Redis, rather than each API host's clock, defines the animation epoch.
        Assert.InRange(Math.Abs(shared.ServerTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), 0, 10000);
    }
}
