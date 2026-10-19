using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure;
using UGTM.Modules.Identity.Infrastructure.Authentication;
using UGTM.Modules.Identity.Infrastructure.Endpoints;
using UGTM.Modules.Identity.Infrastructure.Notifications;
using UGTM.Modules.Identity.Infrastructure.Persistence;

namespace UGTM.Modules.Identity.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class IdentityHttpLifecycleIntegrationTests(PostgreSqlFixture fixture)
{
    private const string InitialPassword = "StrongPassword!123";
    private const string NewPassword = "ResetPassword!456";
    private static readonly DateTime Now =
        new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Http_api_completes_activation_session_and_recovery_lifecycle()
    {
        await using var app = await CreateApplicationAsync();
        var activationToken = await SeedPendingStudentAsync(app.Services);
        await app.StartAsync();

        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);

        using var csrfResponse = await SendAsync(client, cookies, HttpMethod.Get, "/api/identity/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrfToken = ReadString(await csrfResponse.Content.ReadAsStringAsync(), "data", "token");

        using var activationResponse = await SendAsync(
            client,
            cookies,
            HttpMethod.Post,
            "/api/identity/auth/activation/complete",
            new { token = activationToken, password = InitialPassword });
        Assert.Equal(HttpStatusCode.OK, activationResponse.StatusCode);
        AssertNoStore(activationResponse);

        using var loginResponse = await SendAsync(
            client,
            cookies,
            HttpMethod.Post,
            "/api/identity/auth/login",
            new { identifier = "student@ugtm.edu.vn", password = InitialPassword },
            csrfToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        AssertNoStore(loginResponse);
        var firstAccessToken = ReadString(
            await loginResponse.Content.ReadAsStringAsync(), "data", "accessToken");
        Assert.True(cookies.TryGetValue("ugtm-refresh", out var firstRefreshToken));

        using var refreshResponse = await SendAsync(
            client,
            cookies,
            HttpMethod.Post,
            "/api/identity/auth/refresh",
            csrfHeader: csrfToken);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedAccessToken = ReadString(
            await refreshResponse.Content.ReadAsStringAsync(), "data", "accessToken");
        Assert.NotEqual(firstAccessToken, refreshedAccessToken);
        Assert.True(cookies.TryGetValue("ugtm-refresh", out var rotatedRefreshToken));
        Assert.NotEqual(firstRefreshToken, rotatedRefreshToken);

        var sessionId = ParseSessionId(refreshedAccessToken);
        using var revokeResponse = await SendAsync(
            client,
            cookies,
            HttpMethod.Delete,
            $"/api/identity/auth/sessions/{sessionId}",
            bearerToken: refreshedAccessToken);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        Assert.False(cookies.ContainsKey("ugtm-refresh"));

        cookies["ugtm-refresh"] = rotatedRefreshToken!;
        using var rejectedRefresh = await SendAsync(
            client,
            cookies,
            HttpMethod.Post,
            "/api/identity/auth/refresh",
            csrfHeader: csrfToken);
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedRefresh.StatusCode);
        Assert.Equal(
            "Identity.InvalidSession",
            ReadString(await rejectedRefresh.Content.ReadAsStringAsync(), "error", "code"));

        using var recoveryStart = await SendAsync(
            client,
            cookies,
            HttpMethod.Post,
            "/api/identity/auth/recovery/start",
            new { email = "student@ugtm.edu.vn" });
        Assert.Equal(HttpStatusCode.OK, recoveryStart.StatusCode);
        var challengeId = ReadString(
            await recoveryStart.Content.ReadAsStringAsync(), "data", "challengeId");
        var recoveryCode = await ReadRecoveryCodeAsync(app.Services);

        using var recoveryVerify = await SendAsync(
            client,
            cookies,
            HttpMethod.Post,
            "/api/identity/auth/recovery/verify",
            new { challengeId, code = recoveryCode });
        Assert.Equal(HttpStatusCode.OK, recoveryVerify.StatusCode);
        var resetGrant = ReadString(
            await recoveryVerify.Content.ReadAsStringAsync(), "data", "resetGrant");

        using var recoveryReset = await SendAsync(
            client,
            cookies,
            HttpMethod.Post,
            "/api/identity/auth/recovery/reset",
            new { resetGrant, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, recoveryReset.StatusCode);

        using var oldPasswordLogin = await SendAsync(
            client,
            cookies,
            HttpMethod.Post,
            "/api/identity/auth/login",
            new { identifier = "SV0001", password = InitialPassword },
            csrfToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        using var newPasswordLogin = await SendAsync(
            client,
            cookies,
            HttpMethod.Post,
            "/api/identity/auth/login",
            new { identifier = "SV0001", password = NewPassword },
            csrfToken);
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    private async Task<WebApplication> CreateApplicationAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString,
            ["Identity:Jwt:Issuer"] = "UGTM.Tests",
            ["Identity:Jwt:Audience"] = "UGTM.Tests",
            ["Identity:Jwt:PrivateKeyPath"] = "unused-by-test-authentication.pem",
            ["Identity:Jwt:KeyId"] = "identity-http-tests",
            ["Identity:Jwt:AccessTokenMinutes"] = "10",
            ["Identity:Email:PublicWebBaseUrl"] = "https://ugtm.example",
            ["Identity:Email:OutboxPartition"] = "http-tests",
            ["Identity:ClientMetadata:IpHashKey"] = "identity-http-tests-ip-hash-key-32-characters",
            ["Identity:ClientMetadata:UserAgentMaxLength"] = "300"
        });

        var identity = new IdentityModule();
        identity.RegisterModule(builder.Services, builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.RemoveAll<IAccessTokenService>();
        builder.Services.AddScoped<IAccessTokenService, TestAccessTokenService>();
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                _ => { });

        var app = builder.Build();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapIdentityAuthEndpoints();
        return app;
    }

    private static async Task<string> SeedPendingStudentAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS identity CASCADE;");
        await db.Database.EnsureCreatedAsync();

        var tokens = scope.ServiceProvider.GetRequiredService<IOpaqueTokenService>();
        var policy = scope.ServiceProvider.GetRequiredService<IdentitySecurityPolicy>();
        var activationToken = tokens.CreateToken();
        var user = User.ProvisionStudent(
            Guid.NewGuid(),
            "student@ugtm.edu.vn",
            "STUDENT@UGTM.EDU.VN",
            "Student HTTP Lifecycle",
            "SV0001",
            null,
            Now);
        var challenge = OneTimeChallenge.Create(
            user.Id,
            ChallengePurpose.ActivateAccount,
            activationToken.Selector,
            activationToken.SecretHash,
            user.Email,
            policy.ChallengeMaxAttempts,
            Now,
            Now.Add(policy.ActivationLifetime));
        var studentRoleId = await db.Roles
            .Where(role => role.RoleName == SystemRoleName.Student)
            .Select(role => role.Id)
            .SingleAsync();

        db.Users.Add(user);
        db.OneTimeChallenges.Add(challenge);
        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = studentRoleId,
            AssignedAt = Now
        });
        await db.SaveChangesAsync();
        return activationToken.RawToken;
    }

    private static async Task<string> ReadRecoveryCodeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var dataProtection = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var message = await db.IdentityOutboxMessages
            .AsNoTracking()
            .SingleAsync(value => value.Type == "recovery-otp");
        var json = dataProtection
            .CreateProtector(message.ProtectionPurpose)
            .Unprotect(message.ProtectedPayload);
        return JsonSerializer.Deserialize<IdentityEmailPayload>(json)!.Secret;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        IDictionary<string, string> cookies,
        HttpMethod method,
        string path,
        object? body = null,
        string? csrfHeader = null,
        string? bearerToken = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (cookies.Count > 0)
        {
            request.Headers.TryAddWithoutValidation(
                "Cookie",
                string.Join("; ", cookies.Select(value => $"{value.Key}={value.Value}")));
        }

        if (csrfHeader is not null)
        {
            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrfHeader);
        }

        if (bearerToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        var response = await client.SendAsync(request);
        CaptureCookies(response, cookies);
        return response;
    }

    private static void CaptureCookies(
        HttpResponseMessage response,
        IDictionary<string, string> cookies)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return;
        }

        foreach (var value in values)
        {
            var pair = value.Split(';', 2)[0];
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var name = pair[..separator];
            var cookieValue = pair[(separator + 1)..];
            if (cookieValue.Length == 0)
            {
                cookies.Remove(name);
            }
            else
            {
                cookies[name] = cookieValue;
            }
        }
    }

    private static string ReadString(string json, params string[] path)
    {
        using var document = JsonDocument.Parse(json);
        var current = document.RootElement;
        foreach (var segment in path)
        {
            current = current.GetProperty(segment);
        }

        return current.GetString()!;
    }

    private static Guid ParseSessionId(string accessToken)
        => Guid.ParseExact(accessToken.Split('.')[2], "N");

    private static void AssertNoStore(HttpResponseMessage response)
    {
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Contains(response.Headers.Pragma, value => value.Name == "no-cache");
    }

    private sealed class TestAccessTokenService : IAccessTokenService
    {
        public AccessTokenResult Create(UserProfileDto user, Guid sessionId, DateTime now)
            => new(
                $"test.{user.Id:N}.{sessionId:N}.{Guid.NewGuid():N}",
                now.AddMinutes(10));
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "IdentityHttpTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var value = Request.Headers.Authorization.ToString();
            if (!value.StartsWith("Bearer test.", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var parts = value["Bearer ".Length..].Split('.');
            if (parts.Length != 4
                || !Guid.TryParseExact(parts[1], "N", out var userId)
                || !Guid.TryParseExact(parts[2], "N", out var sessionId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid test access token."));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim("sid", sessionId.ToString())
            ], SchemeName));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
