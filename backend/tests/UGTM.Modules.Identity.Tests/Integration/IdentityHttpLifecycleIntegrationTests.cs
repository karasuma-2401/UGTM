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
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;
using UGTM.Modules.Identity.Infrastructure;
using UGTM.Modules.Identity.Infrastructure.Authentication;
using UGTM.Modules.Identity.Infrastructure.Authorization;
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

    [Fact]
    public async Task Http_api_enforces_admin_reset_password_and_must_change_password_lifecycle()
    {
        const string adminPassword = "AdminPassword!123";
        const string studentInitialPassword = "StudentInitial!123";
        const string studentFinalPassword = "StudentFinal!456";

        await using var app = await CreateApplicationAsync(resolveSessionAndRoles: true);
        var (adminEmail, studentId, studentEmail) = await SeedAdminAndStudentAsync(
            app.Services, adminPassword, studentInitialPassword);
        await app.StartAsync();

        var adminCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        using var adminClient = app.GetTestClient();
        adminClient.BaseAddress = new Uri("http://localhost");
        var adminCsrf = ReadString(
            await (await SendAsync(adminClient, adminCookies, HttpMethod.Get, "/api/identity/auth/csrf"))
                .Content.ReadAsStringAsync(),
            "data",
            "token");
        var adminLoginJson = await (await SendAsync(
            adminClient,
            adminCookies,
            HttpMethod.Post,
            "/api/identity/auth/login",
            new { identifier = adminEmail, password = adminPassword },
            adminCsrf)).Content.ReadAsStringAsync();
        var adminToken = ReadString(adminLoginJson, "data", "accessToken");

        var studentCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        using var studentClient = app.GetTestClient();
        studentClient.BaseAddress = new Uri("http://localhost");
        var studentCsrf = ReadString(
            await (await SendAsync(studentClient, studentCookies, HttpMethod.Get, "/api/identity/auth/csrf"))
                .Content.ReadAsStringAsync(),
            "data",
            "token");
        var studentLogin1Json = await (await SendAsync(
            studentClient,
            studentCookies,
            HttpMethod.Post,
            "/api/identity/auth/login",
            new { identifier = studentEmail, password = studentInitialPassword },
            studentCsrf)).Content.ReadAsStringAsync();
        var studentOldToken = ReadString(studentLogin1Json, "data", "accessToken");
        Assert.False(ReadBoolean(studentLogin1Json, "data", "user", "mustChangePassword"));

        // Admin resets student password in Generated mode with requirePasswordChange = true
        using var resetRes = await SendAsync(
            adminClient,
            adminCookies,
            HttpMethod.Post,
            $"/api/users/{studentId}/reset-password",
            new
            {
                mode = "Generated",
                requirePasswordChange = true,
                adminPassword,
                reason = "E2E integration reset"
            },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, resetRes.StatusCode);
        AssertNoStore(resetRes);
        var resetJson = await resetRes.Content.ReadAsStringAsync();
        var tempPassword = ReadString(resetJson, "data", "temporaryPassword");
        Assert.Equal(16, tempPassword.Length);
        Assert.True(ReadBoolean(resetJson, "data", "mustChangePassword"));

        // Old student session is revoked immediately
        using var oldSessionMe = await SendAsync(
            studentClient,
            studentCookies,
            HttpMethod.Get,
            "/api/identity/auth/me",
            bearerToken: studentOldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSessionMe.StatusCode);

        // Alert email in outbox has no password; pending OTP invalidated; audit recorded without password
        await using (var verifyScope = app.Services.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var dp = verifyScope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
            var alertMsg = await db.IdentityOutboxMessages
                .AsNoTracking()
                .SingleAsync(m => m.Type == "admin-password-reset");
            var decrypted = dp.CreateProtector(alertMsg.ProtectionPurpose).Unprotect(alertMsg.ProtectedPayload);
            var payload = JsonSerializer.Deserialize<IdentityEmailPayload>(decrypted)!;
            Assert.Equal("must-change", payload.Secret);
            Assert.DoesNotContain(tempPassword, decrypted);

            var pendingChallenges = await db.OneTimeChallenges
                .AsNoTracking()
                .CountAsync(c => c.UserId == studentId && c.ConsumedAt == null && c.InvalidatedAt == null);
            Assert.Equal(0, pendingChallenges);

            var resetAudit = await db.AuditLogs
                .AsNoTracking()
                .SingleAsync(a => a.Action == AuditActions.UserPasswordResetByAdmin);
            Assert.DoesNotContain(tempPassword, resetAudit.MetadataJson ?? string.Empty);
        }

        // Student logs in with temporary password -> mustChangePassword = true
        var studentLogin2Res = await SendAsync(
            studentClient,
            studentCookies,
            HttpMethod.Post,
            "/api/identity/auth/login",
            new { identifier = studentEmail, password = tempPassword },
            studentCsrf);
        Assert.Equal(HttpStatusCode.OK, studentLogin2Res.StatusCode);
        var studentLogin2Json = await studentLogin2Res.Content.ReadAsStringAsync();
        var studentTempToken = ReadString(studentLogin2Json, "data", "accessToken");
        Assert.True(ReadBoolean(studentLogin2Json, "data", "user", "mustChangePassword"));

        // Hard gate blocks /sessions with 403 Identity.PasswordChangeRequired, allows /me
        using var blockedSessions = await SendAsync(
            studentClient,
            studentCookies,
            HttpMethod.Get,
            "/api/identity/auth/sessions",
            bearerToken: studentTempToken);
        Assert.Equal(HttpStatusCode.Forbidden, blockedSessions.StatusCode);
        Assert.Equal("Identity.PasswordChangeRequired", ReadString(await blockedSessions.Content.ReadAsStringAsync(), "error", "code"));

        using var allowedMe = await SendAsync(
            studentClient,
            studentCookies,
            HttpMethod.Get,
            "/api/identity/auth/me",
            bearerToken: studentTempToken);
        Assert.Equal(HttpStatusCode.OK, allowedMe.StatusCode);
        Assert.True(ReadBoolean(await allowedMe.Content.ReadAsStringAsync(), "data", "mustChangePassword"));

        // Student completes mandatory change-password -> allowed, clears flag, revokes temp session
        using var changePassRes = await SendAsync(
            studentClient,
            studentCookies,
            HttpMethod.Post,
            "/api/identity/auth/change-password",
            new { currentPassword = tempPassword, newPassword = studentFinalPassword },
            bearerToken: studentTempToken);
        Assert.Equal(HttpStatusCode.OK, changePassRes.StatusCode);

        using var revokedTempToken = await SendAsync(
            studentClient,
            studentCookies,
            HttpMethod.Get,
            "/api/identity/auth/me",
            bearerToken: studentTempToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedTempToken.StatusCode);

        // Student logs in with final password -> mustChangePassword = false & full API access restored
        var studentLogin3Res = await SendAsync(
            studentClient,
            studentCookies,
            HttpMethod.Post,
            "/api/identity/auth/login",
            new { identifier = studentEmail, password = studentFinalPassword },
            studentCsrf);
        Assert.Equal(HttpStatusCode.OK, studentLogin3Res.StatusCode);
        var studentLogin3Json = await studentLogin3Res.Content.ReadAsStringAsync();
        var studentFinalToken = ReadString(studentLogin3Json, "data", "accessToken");
        Assert.False(ReadBoolean(studentLogin3Json, "data", "user", "mustChangePassword"));

        using var allowedSessions = await SendAsync(
            studentClient,
            studentCookies,
            HttpMethod.Get,
            "/api/identity/auth/sessions",
            bearerToken: studentFinalToken);
        Assert.Equal(HttpStatusCode.OK, allowedSessions.StatusCode);
    }

    private async Task<WebApplication> CreateApplicationAsync(bool resolveSessionAndRoles = false)
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
        builder.Services.RemoveAll<IClock>();
        builder.Services.RemoveAll<IAccessTokenService>();
        builder.Services.AddSingleton<IClock>(new FixedClock(Now));
        builder.Services.AddScoped<IAccessTokenService, TestAccessTokenService>();
        builder.Services.AddSingleton(new TestAuthOptions(resolveSessionAndRoles));
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
        app.UseIdentityPasswordChangeEnforcement();
        app.UseAuthorization();
        app.MapIdentityAuthEndpoints();
        app.MapIdentityUserEndpoints();
        return app;
    }

    private static async Task<(string AdminEmail, Guid StudentId, string StudentEmail)> SeedAdminAndStudentAsync(
        IServiceProvider services,
        string adminPassword,
        string studentPassword)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpaqueTokenService>();
        var policy = scope.ServiceProvider.GetRequiredService<IdentitySecurityPolicy>();

        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS identity CASCADE;");
        await db.Database.EnsureCreatedAsync();

        var admin = User.Create(
            Guid.NewGuid(),
            "admin.reset@ugtm.edu.vn",
            "ADMIN.RESET@UGTM.EDU.VN",
            "Admin Reset Tester",
            "AD900",
            null,
            null,
            AccountStatus.PendingActivation,
            Now);
        admin.Activate(passwords.Hash(admin, adminPassword), Now);

        var student = User.ProvisionStudent(
            Guid.NewGuid(),
            "student.reset@ugtm.edu.vn",
            "STUDENT.RESET@UGTM.EDU.VN",
            "Student Reset Target",
            "SV9001",
            null,
            Now);
        student.Activate(passwords.Hash(student, studentPassword), Now);

        var recoveryOtp = tokens.CreateNumericCode(6);
        var pendingChallenge = OneTimeChallenge.Create(
            student.Id,
            ChallengePurpose.PasswordResetOtp,
            recoveryOtp.Selector,
            recoveryOtp.SecretHash,
            student.Email,
            policy.ChallengeMaxAttempts,
            Now,
            Now.Add(policy.RecoveryOtpLifetime));

        var adminRoleId = await db.Roles
            .Where(r => r.RoleName == nameof(SystemRoleName.Admin))
            .Select(r => r.Id)
            .SingleAsync();
        var studentRoleId = await db.Roles
            .Where(r => r.RoleName == nameof(SystemRoleName.Student))
            .Select(r => r.Id)
            .SingleAsync();

        db.Users.AddRange(admin, student);
        db.OneTimeChallenges.Add(pendingChallenge);
        db.UserRoles.AddRange(
            new UserRole { UserId = admin.Id, RoleId = adminRoleId, AssignedAt = Now },
            new UserRole { UserId = student.Id, RoleId = studentRoleId, AssignedAt = Now });
        await db.SaveChangesAsync();

        return (admin.Email, student.Id, student.Email);
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
            .Where(role => role.RoleName == nameof(SystemRoleName.Student))
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

    private static bool ReadBoolean(string json, params string[] path)
    {
        using var document = JsonDocument.Parse(json);
        var current = document.RootElement;
        foreach (var segment in path)
        {
            current = current.GetProperty(segment);
        }

        return current.GetBoolean();
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

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    private sealed record TestAuthOptions(bool ResolveSessionAndRoles);

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestAuthOptions testAuthOptions)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "IdentityHttpTest";

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var value = Request.Headers.Authorization.ToString();
            if (!value.StartsWith("Bearer test.", StringComparison.Ordinal))
            {
                return AuthenticateResult.NoResult();
            }

            var parts = value["Bearer ".Length..].Split('.');
            if (parts.Length != 4
                || !Guid.TryParseExact(parts[1], "N", out var userId)
                || !Guid.TryParseExact(parts[2], "N", out var sessionId))
            {
                return AuthenticateResult.Fail("Invalid test access token.");
            }

            var claims = new List<Claim>
            {
                new("sub", userId.ToString()),
                new("sid", sessionId.ToString())
            };

            if (testAuthOptions.ResolveSessionAndRoles)
            {
                var sessions = Context.RequestServices.GetRequiredService<IAuthenticationSessionRepository>();
                var profiles = Context.RequestServices.GetRequiredService<IUserProfileReader>();
                var clock = Context.RequestServices.GetRequiredService<IClock>();

                var session = await sessions.GetSessionAsync(sessionId, Context.RequestAborted);
                if (session is null
                    || session.UserId != userId
                    || session.User.Status != AccountStatus.Active
                    || !session.CanRefresh(session.User.SecurityStamp, clock.UtcNow))
                {
                    return AuthenticateResult.Fail("Session is no longer active.");
                }

                var profile = await profiles.GetAsync(userId, Context.RequestAborted);
                if (profile is null)
                {
                    return AuthenticateResult.Fail("Profile not found.");
                }

                Context.Items[PasswordChangeRequiredMiddleware.HttpContextItemKey] = session.User.MustChangePassword;
                if (profile.MustChangePassword)
                {
                    claims.Add(new Claim(IdentityClaimTypes.PasswordChangeRequired, "true"));
                }

                claims.AddRange(profile.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
                claims.AddRange(profile.Permissions.Select(p => new Claim("permission", p)));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return AuthenticateResult.Success(
                new AuthenticationTicket(principal, SchemeName));
        }
    }
}
