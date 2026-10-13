using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using UGTM.Modules.Identity.Infrastructure.RateLimiting;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task Login_policy_returns_429_and_retry_after_when_limit_is_exceeded()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddIdentityRateLimits();

        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapGet("/login", () => Results.Ok())
            .RequireRateLimiting(IdentityRateLimitPolicies.Login);
        await app.StartAsync();

        using var client = app.GetTestClient();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var accepted = await client.GetAsync("/login");
            Assert.True(accepted.IsSuccessStatusCode);
        }

        using var rejected = await client.GetAsync("/login");

        Assert.Equal(429, (int)rejected.StatusCode);
        Assert.True(rejected.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.TryParse(Assert.Single(values), out var seconds));
        Assert.True(seconds > 0);
    }
}
