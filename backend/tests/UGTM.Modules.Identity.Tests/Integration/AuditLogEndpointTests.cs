using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Infrastructure.Endpoints;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.Identity.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class AuditLogEndpointTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Endpoints_enforce_admin_and_share_filters_for_list_statistics_and_csv()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        DatabaseOptions.Configure(options, fixture.ConnectionString);
        await using (var db = new IdentityDbContext(options.Options))
        {
            await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS identity CASCADE;");
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            db.AuditLogs.Add(AuditLog.Create(null, null, "User", "=1+1", "Identity.User.Created", "Succeeded", null,
                "request-test", "{\"note\":\"sample\"}", DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<IdentityDbContext>(o => DatabaseOptions.Configure(o, fixture.ConnectionString));
        builder.Services.AddScoped<IAuditLogQueryReader, AuditLogQueryReader>();
        builder.Services.AddScoped<GetAuditLogsHandler>();
        builder.Services.AddAuthentication("AuditTest").AddScheme<AuthenticationSchemeOptions, AuditTestAuthentication>("AuditTest", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin")));
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapIdentityAuditLogEndpoints();
        await app.StartAsync();
        var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/audit-logs")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("AuditTest", "user");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit-logs/statistics")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("AuditTest", "admin");
        var listResponse = await client.GetAsync("/api/audit-logs?searchTerm=sample&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, list.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32());
        var id = list.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/audit-logs/{id}")).StatusCode);
        var statsResponse = await client.GetAsync("/api/audit-logs/statistics?searchTerm=sample");
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        using var stats = JsonDocument.Parse(await statsResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, stats.RootElement.GetProperty("data").GetProperty("success").GetInt32());
        var csv = await client.GetAsync("/api/audit-logs/export?requestId=request-test");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Contains("\"'=1+1\"", await csv.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/audit-logs?outcome=invalid")).StatusCode);
    }

    private sealed class AuditTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (header is not ("AuditTest admin" or "AuditTest user")) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test"), new Claim(ClaimTypes.Role, header == "AuditTest admin" ? "Admin" : "Lecturer")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
