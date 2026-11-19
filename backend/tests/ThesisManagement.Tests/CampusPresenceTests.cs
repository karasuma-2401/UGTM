using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UGTM.Api.Campus;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class CampusPresenceTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static CampusHeartbeat Join(Guid? visitor = null) => new(visitor ?? Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task Guests_are_shared_without_counting_tabs_twice()
    {
        var store = new MemoryCampusPresenceStore(new Clock());
        var first = Join();
        await store.HeartbeatAsync(first, null);
        var secondTab = Join(first.VisitorId);
        Assert.Single((await store.HeartbeatAsync(secondTab, null)).Visitors);
        var secondPerson = Join();
        var snapshot = await store.HeartbeatAsync(secondPerson, null);
        Assert.Equal(2, snapshot.Visitors.Length);
        Assert.Equal(snapshot.Visitors, (await store.HeartbeatAsync(first, null)).Visitors);
        Assert.Equal(2, (await store.HeartbeatAsync(secondTab with { Leaving = true }, null)).Visitors.Length);
        Assert.Single((await store.HeartbeatAsync(first with { Leaving = true }, null)).Visitors);
    }

    [Fact]
    public async Task Login_replaces_guest_and_deduplicates_account_across_browsers_without_exposing_identity()
    {
        var store = new MemoryCampusPresenceStore(new Clock());
        var first = Join();
        var otherTab = Join(first.VisitorId);
        await store.HeartbeatAsync(first, null);
        await store.HeartbeatAsync(otherTab, null);
        var accountId = Guid.NewGuid().ToString();
        var signedIn = await store.HeartbeatAsync(first, accountId);
        Assert.Single(signedIn.Visitors);
        Assert.Equal(signedIn.Visitors, (await store.HeartbeatAsync(Join(), accountId)).Visitors);
        Assert.DoesNotContain(accountId, JsonSerializer.Serialize(signedIn));
        Assert.DoesNotContain("email", JsonSerializer.Serialize(signedIn), StringComparison.OrdinalIgnoreCase);
        var loggedOut = await store.HeartbeatAsync(first, null);
        Assert.Equal(2, loggedOut.Visitors.Length); // The other browser remains signed in.
    }

    [Fact]
    public async Task Heartbeat_refreshes_lease_and_abrupt_disconnect_expires_without_other_clients_losing_their_lease()
    {
        var clock = new Clock();
        var store = new MemoryCampusPresenceStore(clock);
        var first = Join(); var second = Join();
        await store.HeartbeatAsync(first, null);
        clock.Now = clock.Now.AddSeconds(50);
        Assert.Equal(2, (await store.HeartbeatAsync(second, null)).Visitors.Length);
        clock.Now = clock.Now.AddSeconds(26);
        Assert.Single((await store.HeartbeatAsync(second, null)).Visitors);
        clock.Now = clock.Now.AddSeconds(CampusPresence.LeaseSeconds);
        Assert.Empty((await store.HeartbeatAsync(second with { Leaving = true }, null)).Visitors);
    }

    [Fact]
    public async Task Concurrent_clients_preserve_all_visitors()
    {
        var store = new MemoryCampusPresenceStore(new Clock());
        var clients = Enumerable.Range(0, 100).Select(_ => Join()).ToArray();
        await Task.WhenAll(clients.Select(client => Task.Run(() => store.HeartbeatAsync(client, null))));
        Assert.Equal(100, (await store.HeartbeatAsync(clients[0], null)).Visitors.Length);
    }

    [Fact]
    public async Task Endpoint_validates_input_uses_server_identity_and_shares_roster_between_http_clients()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddCampusPresence(new ConfigurationBuilder().Build());
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseRateLimiter();
        app.Use(async (context, next) =>
        {
            // Test authentication boundary; production uses the existing JWT/session validator.
            if (context.Request.Headers.Authorization == "Bearer valid-test-session")
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "private-account-id")], "test"));
            await next(context);
        });
        app.UseAuthorization();
        app.MapCampusPresence();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var bad = await client.PostAsJsonAsync("/api/campus/presence", new CampusHeartbeat(Guid.Empty, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var first = Join();
        var joined = await client.PostAsJsonAsync("/api/campus/presence", first);
        Assert.True(joined.IsSuccessStatusCode);
        Assert.True(joined.Headers.CacheControl?.NoStore);
        Assert.Single((await joined.Content.ReadFromJsonAsync<CampusSnapshot>())!.Visitors);
        Assert.Equal(2, (await (await client.PostAsJsonAsync("/api/campus/presence", Join())).Content.ReadFromJsonAsync<CampusSnapshot>())!.Visitors.Length);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/campus/presence", first)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "valid-test-session");
        var authenticated = await (await client.PostAsJsonAsync("/api/campus/presence", first)).Content.ReadFromJsonAsync<CampusSnapshot>();
        Assert.Equal(2, authenticated!.Visitors.Length);
        Assert.DoesNotContain("private-account-id", JsonSerializer.Serialize(authenticated));
    }
}
