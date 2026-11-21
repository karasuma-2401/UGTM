using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Application.Users.CreateUser;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Application.Users.GetUsers;
using UGTM.Modules.Identity.Application.Users.ImportUsers;
using UGTM.Modules.Identity.Application.Users.ResetPassword;
using UGTM.Modules.Identity.Application.Users.UpdateUser;
using UGTM.Modules.Identity.Application.Users.UpdateUserStatus;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;
using UGTM.Modules.Identity.Infrastructure.Authentication;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.Identity.Infrastructure.Endpoints;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class UserEndpointsQaTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly QaUserStore _store = new();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        builder.Services.AddSingleton(_store);
        builder.Services.AddSingleton<IUserRepository>(_store);
        builder.Services.AddSingleton<IUserQueryReader>(_store);
        builder.Services.AddSingleton<IIdentityUnitOfWork>(_store);
        builder.Services.AddSingleton<IAuditLogWriter>(_store);
        builder.Services.AddSingleton<IOneTimeChallengeRepository>(_store);
        builder.Services.AddSingleton<IIdentityEmailOutbox>(_store);
        builder.Services.AddSingleton<IOpaqueTokenService>(_store);
        builder.Services.AddSingleton<IPasswordPolicy>(_store);
        builder.Services.AddSingleton<IPasswordService>(_store);
        builder.Services.AddSingleton<ITemporaryPasswordGenerator, CryptoTemporaryPasswordGenerator>();
        builder.Services.AddSingleton<IAuthenticationSessionRepository>(_store);
        builder.Services.AddSingleton<IReauthenticationVerifier>(_store);
        builder.Services.AddSingleton<IClock>(_store);
        builder.Services.AddSingleton(new IdentitySecurityPolicy());
        builder.Services.AddSingleton<IUserImportFileReader>(_store);
        builder.Services.AddSingleton<IUserImportTemplateGenerator>(_store);

        builder.Services.AddScoped<GetUsersHandler>();
        builder.Services.AddScoped<CreateUserHandler>();
        builder.Services.AddScoped<UpdateUserHandler>();
        builder.Services.AddScoped<ImportUsersHandler>();
        builder.Services.AddScoped<UpdateUserStatusHandler>();
        builder.Services.AddScoped<AdminResetPasswordHandler>();

        builder.Services.AddAuthentication(QaUserAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, QaUserAuthHandler>(QaUserAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseIdentityPasswordChangeEnforcement();
        _app.UseAuthorization();
        _app.MapIdentityUserEndpoints();
        _app.MapGet("/api/qa/allowed-when-must-change", () => Results.Ok(ApiResponse.Ok()))
            .RequireAuthorization()
            .AllowWhenPasswordChangeRequired();
        _app.MapGet("/api/qa/anonymous", () => Results.Ok(ApiResponse.Ok()))
            .AllowAnonymous();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private void Authenticate(Guid userId, string[] roles, params string[] permissions)
    {
        var claims = new List<string> { $"sub={userId}" };
        claims.AddRange(roles.Select(r => $"role={r}"));
        claims.AddRange(permissions.Select(p => $"perm={p}"));
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(QaUserAuthHandler.SchemeName, string.Join(";", claims));
    }

    private void AuthenticateAsAdmin() => Authenticate(Guid.NewGuid(), ["Admin"]);
    private void ClearAuth() => _client.DefaultRequestHeaders.Authorization = null;

    private async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("success").GetBoolean(), $"Expected success response, got: {json}");
        return json.GetProperty("data");
    }

    private async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object
            ? err.GetProperty("code").GetString() ?? string.Empty
            : string.Empty;
    }

    #region A. AuthN & AuthZ

    [Fact]
    public async Task UserEndpoints_WhenUnauthenticated_Return401()
    {
        ClearAuth();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/users", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PatchAsJsonAsync($"/api/users/{Guid.NewGuid()}/status", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync($"/api/users/{Guid.NewGuid()}/reset-password", new { })).StatusCode);
    }

    [Fact]
    public async Task UserEndpoints_WithoutRequiredPermissions_Return403()
    {
        Authenticate(Guid.NewGuid(), ["Lecturer"], PermissionCodes.IdentityUsersRead);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync("/api/users", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PatchAsJsonAsync($"/api/users/{Guid.NewGuid()}/status", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync($"/api/users/{Guid.NewGuid()}/reset-password", new { })).StatusCode);

        Authenticate(Guid.NewGuid(), ["Student"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/users")).StatusCode);
    }

    #endregion

    #region B. Happy Path E2E & Last Admin Guard

    [Fact]
    public async Task FullUserLifecycle_CreateWithAndWithoutPassword_Filter_Update_StatusTransitions_AndLastAdminGuard()
    {
        AuthenticateAsAdmin();

        // 1. Create active user with initialPassword
        var createActive = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "admin.one@ugtm.edu.vn",
            fullName = "  Admin One  ",
            userCode = "ad001",
            phoneNumber = "0901234567",
            roles = new[] { "Admin" },
            initialPassword = "StrongPassword!123"
        });
        Assert.Equal(HttpStatusCode.OK, createActive.StatusCode);
        var adminDto = await ReadDataAsync(createActive);
        var adminId = adminDto.GetProperty("id").GetGuid();
        Assert.Equal("Active", adminDto.GetProperty("status").GetString());
        Assert.Equal("AD001", adminDto.GetProperty("userCode").GetString());

        // 2. Last active admin cannot be suspended or demoted
        var suspendLastAdmin = await _client.PatchAsJsonAsync($"/api/users/{adminId}/status", new { status = "Suspended" });
        Assert.Equal(HttpStatusCode.BadRequest, suspendLastAdmin.StatusCode);
        Assert.Equal("Identity.CannotDisableLastAdmin", await ReadErrorCodeAsync(suspendLastAdmin));

        var demoteLastAdmin = await _client.PutAsJsonAsync($"/api/users/{adminId}", new
        {
            fullName = "Admin One",
            userCode = "AD001",
            phoneNumber = "0901234567",
            roles = new[] { "Faculty" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, demoteLastAdmin.StatusCode);
        Assert.Equal("Identity.CannotRemoveLastAdminRole", await ReadErrorCodeAsync(demoteLastAdmin));

        // 3. Create pending student without password
        var createStudent = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "sv.qa@ugtm.edu.vn",
            fullName = "Sinh Viên QA",
            userCode = "22520001",
            roles = new[] { "Student" },
            sendActivationEmail = true
        });
        Assert.Equal(HttpStatusCode.OK, createStudent.StatusCode);
        var studentDto = await ReadDataAsync(createStudent);
        var studentId = studentDto.GetProperty("id").GetGuid();
        Assert.Equal("PendingActivation", studentDto.GetProperty("status").GetString());

        // PendingActivation cannot transition directly to Suspended
        var suspendPending = await _client.PatchAsJsonAsync($"/api/users/{studentId}/status", new { status = "Suspended" });
        Assert.Equal(HttpStatusCode.BadRequest, suspendPending.StatusCode);
        Assert.Equal("Identity.InvalidStatusTransition", await ReadErrorCodeAsync(suspendPending));

        // 4. Duplicate email or userCode returns 409 Conflict
        var dupEmail = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "SV.QA@ugtm.edu.vn",
            fullName = "Dup",
            roles = new[] { "Student" },
            sendActivationEmail = false
        });
        Assert.Equal(HttpStatusCode.Conflict, dupEmail.StatusCode);
        Assert.Equal("Identity.DuplicateAccount", await ReadErrorCodeAsync(dupEmail));

        var dupCode = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "other@ugtm.edu.vn",
            fullName = "Dup Code",
            userCode = "22520001",
            roles = new[] { "Student" },
            sendActivationEmail = false
        });
        Assert.Equal(HttpStatusCode.Conflict, dupCode.StatusCode);
        Assert.Equal("Identity.DuplicateAccount", await ReadErrorCodeAsync(dupCode));

        // 5. Create second lecturer and exercise status transitions Active -> Suspended -> Active -> Disabled
        var createLec = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "gv.qa@ugtm.edu.vn",
            fullName = "GV QA",
            userCode = "GV888",
            roles = new[] { "Lecturer" },
            initialPassword = "StrongPassword!123"
        });
        var lecId = (await ReadDataAsync(createLec)).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await _client.PatchAsJsonAsync($"/api/users/{lecId}/status", new { status = "Suspended" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PatchAsJsonAsync($"/api/users/{lecId}/status", new { status = "Disabled" })).StatusCode);
        var disabledToSuspended = await _client.PatchAsJsonAsync($"/api/users/{lecId}/status", new { status = "Suspended" });
        Assert.Equal(HttpStatusCode.BadRequest, disabledToSuspended.StatusCode);
        Assert.Equal("Identity.InvalidStatusTransition", await ReadErrorCodeAsync(disabledToSuspended));

        // 6. Query filters: Student vs Staff accountGroup
        var staffRes = await ReadDataAsync(await _client.GetAsync("/api/users?accountGroup=Staff"));
        Assert.Equal(2, staffRes.GetProperty("totalCount").GetInt32());

        var studentGroupRes = await ReadDataAsync(await _client.GetAsync("/api/users?accountGroup=Student"));
        Assert.Equal(1, studentGroupRes.GetProperty("totalCount").GetInt32());
    }

    #endregion

    #region C. Type Mismatch, Malformed JSON & Invalid Route/Query Params

    [Theory]
    [InlineData("/api/users?pageNumber=abc")]
    [InlineData("/api/users?pageSize=xyz")]
    [InlineData("/api/users?role=999")]
    [InlineData("/api/users?role=GhostRole")]
    [InlineData("/api/users?status=0")]
    [InlineData("/api/users?status=Unknown")]
    [InlineData("/api/users?accountGroup=1")]
    [InlineData("/api/users?sortBy=password")]
    [InlineData("/api/users?sortOrder=random")]
    public async Task GetUsers_WithInvalidQueryTypesOrFilters_Returns400BadRequestNot500(string url)
    {
        AuthenticateAsAdmin();
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("PUT", "/api/users/not-a-guid")]
    [InlineData("PATCH", "/api/users/not-a-guid/status")]
    [InlineData("POST", "/api/users/not-a-guid/reset-password")]
    public async Task UserEndpoints_WithNonGuidRouteId_Return404NotFound(string method, string path)
    {
        AuthenticateAsAdmin();
        using var req = new HttpRequestMessage(new HttpMethod(method), path);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"email": "a@b.com", "fullName": "Test", "roles": "Lecturer"}""")]
    [InlineData("""{"email": "a@b.com", "fullName": "Test", "roles": [123]}""")]
    [InlineData("""{"email": "a@b.com", "fullName": 123, "roles": ["Lecturer"]}""")]
    [InlineData("""{"email": "a@b.com", "fullName": "Test", "roles": ["Lecturer"], "sendActivationEmail": "yes"}""")]
    [InlineData("""{bad json""")]
    [InlineData("")]
    public async Task CreateUser_WithMalformedOrTypeMismatchedJson_Returns400BadRequestNot500(string rawJson)
    {
        AuthenticateAsAdmin();
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/users", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithUnsupportedMediaType_Returns415()
    {
        AuthenticateAsAdmin();
        using var content = new StringContent("""{"email":"a@b.com"}""", Encoding.UTF8, "text/plain");
        var response = await _client.PostAsync("/api/users", content);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"status": 1}""")]
    [InlineData("""{invalid""")]
    [InlineData("""{}""")]
    [InlineData("""{"status": null}""")]
    [InlineData("""{"status": "999"}""")]
    [InlineData("""{"status": "PendingActivation"}""")]
    public async Task UpdateUserStatus_WithInvalidOrMalformedPayload_Returns400BadRequestNot500(string rawJson)
    {
        AuthenticateAsAdmin();
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        var response = await _client.PatchAsync($"/api/users/{Guid.NewGuid()}/status", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region D. Empty/Null Payloads, Boundaries & Non-existent References

    [Theory]
    [InlineData("""{}""")]
    [InlineData("null")]
    [InlineData("""{"email":"bad-email","fullName":"Test","roles":["Lecturer"]}""")]
    [InlineData("""{"email":"valid@ugtm.edu.vn","fullName":"   ","roles":["Lecturer"]}""")]
    public async Task CreateUser_WithInvalidEmailOrEmptyFullName_Returns400InvalidInput(string rawJson)
    {
        AuthenticateAsAdmin();
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/users", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Identity.InvalidInput", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task CreateUser_PhonePasswordAndLengthBoundaries_AreValidated()
    {
        AuthenticateAsAdmin();

        var badPhone = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "p1@ugtm.edu.vn",
            fullName = "Phone Fail",
            phoneNumber = "12345",
            roles = new[] { "Lecturer" },
            sendActivationEmail = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, badPhone.StatusCode);
        Assert.Equal("Identity.InvalidPhoneNumber", await ReadErrorCodeAsync(badPhone));

        var weakPass = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "p2@ugtm.edu.vn",
            fullName = "Weak Pass",
            roles = new[] { "Lecturer" },
            initialPassword = "weak"
        });
        Assert.Equal(HttpStatusCode.BadRequest, weakPass.StatusCode);
        Assert.Equal("Identity.WeakPassword", await ReadErrorCodeAsync(weakPass));

        var longName = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "p3@ugtm.edu.vn",
            fullName = new string('A', 151),
            roles = new[] { "Lecturer" },
            sendActivationEmail = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, longName.StatusCode);
        Assert.Equal("Identity.InvalidInput", await ReadErrorCodeAsync(longName));
    }

    [Fact]
    public async Task UpdateAndStatusEndpoints_WithNonExistentUserId_Return404UserNotFound()
    {
        AuthenticateAsAdmin();
        var missingId = Guid.NewGuid();

        var updateRes = await _client.PutAsJsonAsync($"/api/users/{missingId}", new
        {
            fullName = "Missing User",
            roles = new[] { "Lecturer" }
        });
        Assert.Equal(HttpStatusCode.NotFound, updateRes.StatusCode);
        Assert.Equal("Identity.UserNotFound", await ReadErrorCodeAsync(updateRes));

        var statusRes = await _client.PatchAsJsonAsync($"/api/users/{missingId}/status", new { status = "Active" });
        Assert.Equal(HttpStatusCode.NotFound, statusRes.StatusCode);
        Assert.Equal("Identity.UserNotFound", await ReadErrorCodeAsync(statusRes));

        var resetRes = await _client.PostAsJsonAsync($"/api/users/{missingId}/reset-password", new
        {
            mode = "Generated",
            adminPassword = "AdminPassword!123"
        });
        Assert.Equal(HttpStatusCode.NotFound, resetRes.StatusCode);
        Assert.Equal("Identity.UserNotFound", await ReadErrorCodeAsync(resetRes));
    }

    [Fact]
    public async Task AdminResetPassword_HttpLifecycle_GeneratedCustom_Headers_Guards_And_MustChangeEnforcement()
    {
        var adminId = Guid.NewGuid();
        Authenticate(adminId, ["Admin"]);

        var createTarget = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "target.reset@ugtm.edu.vn",
            fullName = "Target User",
            roles = new[] { "Lecturer" },
            initialPassword = "OldPassword!123"
        });
        var targetId = (await ReadDataAsync(createTarget)).GetProperty("id").GetGuid();

        var createPeerAdmin = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "peer.admin@ugtm.edu.vn",
            fullName = "Peer Admin",
            roles = new[] { "Admin" },
            initialPassword = "OldPassword!123"
        });
        var peerAdminId = (await ReadDataAsync(createPeerAdmin)).GetProperty("id").GetGuid();

        // 1. Generated mode returns 16-char temporaryPassword once and Cache-Control: no-store
        var genRes = await _client.PostAsJsonAsync($"/api/users/{targetId}/reset-password", new
        {
            mode = "Generated",
            requirePasswordChange = true,
            adminPassword = "AdminPassword!123",
            reason = "Hỗ trợ cấp lại mật khẩu"
        });
        Assert.Equal(HttpStatusCode.OK, genRes.StatusCode);
        Assert.Contains("no-store", genRes.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        var genData = await ReadDataAsync(genRes);
        var tempPassword = genData.GetProperty("temporaryPassword").GetString();
        Assert.False(string.IsNullOrWhiteSpace(tempPassword));
        Assert.Equal(16, tempPassword!.Length);
        Assert.True(genData.GetProperty("mustChangePassword").GetBoolean());
        Assert.True(genData.GetProperty("notificationEmailQueued").GetBoolean());
        Assert.Contains(_store.RevokedUsers, id => id == targetId);

        // 2. Custom mode returns temporaryPassword = null
        var customRes = await _client.PostAsJsonAsync($"/api/users/{targetId}/reset-password", new
        {
            mode = "Custom",
            newPassword = "NewStrong!Password456",
            requirePasswordChange = false,
            adminPassword = "AdminPassword!123"
        });
        Assert.Equal(HttpStatusCode.OK, customRes.StatusCode);
        var customData = await ReadDataAsync(customRes);
        Assert.Equal(JsonValueKind.Null, customData.GetProperty("temporaryPassword").ValueKind);
        Assert.False(customData.GetProperty("mustChangePassword").GetBoolean());

        // 3. Self reset rejected (400)
        var selfRes = await _client.PostAsJsonAsync($"/api/users/{adminId}/reset-password", new
        {
            mode = "Generated",
            adminPassword = "AdminPassword!123"
        });
        Assert.Equal(HttpStatusCode.BadRequest, selfRes.StatusCode);
        Assert.Equal("Identity.CannotResetOwnPassword", await ReadErrorCodeAsync(selfRes));

        // 4. Non-Admin with identity.users.manage blocked from resetting an Admin account (403)
        Authenticate(Guid.NewGuid(), ["Faculty"], PermissionCodes.IdentityUsersManage);
        var escalateRes = await _client.PostAsJsonAsync($"/api/users/{peerAdminId}/reset-password", new
        {
            mode = "Generated",
            adminPassword = "AdminPassword!123"
        });
        Assert.Equal(HttpStatusCode.Forbidden, escalateRes.StatusCode);
        Assert.Equal("Identity.CannotManageAdminPassword", await ReadErrorCodeAsync(escalateRes));

        // 5. Wrong / locked admin re-authentication
        AuthenticateAsAdmin();
        _store.ReauthOutcome = ReauthenticationOutcome.InvalidCredentials;
        var badReauth = await _client.PostAsJsonAsync($"/api/users/{targetId}/reset-password", new
        {
            mode = "Generated",
            adminPassword = "WrongPassword!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, badReauth.StatusCode);
        Assert.Equal("Identity.ReauthenticationFailed", await ReadErrorCodeAsync(badReauth));

        _store.ReauthOutcome = ReauthenticationOutcome.AccountLocked;
        var lockedReauth = await _client.PostAsJsonAsync($"/api/users/{targetId}/reset-password", new
        {
            mode = "Generated",
            adminPassword = "AdminPassword!123"
        });
        Assert.Equal((HttpStatusCode)429, lockedReauth.StatusCode);
        Assert.Equal("Identity.AccountLocked", await ReadErrorCodeAsync(lockedReauth));
        _store.ReauthOutcome = ReauthenticationOutcome.Verified;

        // 6. PasswordChangeRequiredMiddleware hard gate: blocks business endpoints with 403, allows Allowed & Anonymous
        Authenticate(targetId, ["Admin"], PermissionCodes.IdentityUsersRead);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            QaUserAuthHandler.SchemeName,
            $"sub={targetId};role=Admin;perm={PermissionCodes.IdentityUsersRead};pwd_change=true");

        var blockedUsers = await _client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, blockedUsers.StatusCode);
        Assert.Equal("Identity.PasswordChangeRequired", await ReadErrorCodeAsync(blockedUsers));
        Assert.Contains("no-store", blockedUsers.Headers.CacheControl!.ToString(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/qa/allowed-when-must-change")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/qa/anonymous")).StatusCode);
    }

    [Theory]
    [InlineData("""{"mode": 123, "adminPassword": "AdminPassword!123"}""")]
    [InlineData("""{"mode": "Generated", "requirePasswordChange": "yes", "adminPassword": "AdminPassword!123"}""")]
    [InlineData("""{"mode": "0", "adminPassword": "AdminPassword!123"}""")]
    [InlineData("""{"mode": "Default", "adminPassword": "AdminPassword!123"}""")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{bad json")]
    public async Task AdminResetPassword_WithInvalidTypesOrMalformedJson_Returns400BadRequestNot500(string rawJson)
    {
        AuthenticateAsAdmin();
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/api/users/{Guid.NewGuid()}/reset-password", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    private sealed class QaUserAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "UserQaScheme";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
                return Task.FromResult(AuthenticateResult.NoResult());

            var header = headerValues.ToString();
            if (!header.StartsWith($"{SchemeName} ", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(AuthenticateResult.NoResult());

            var payload = header[(SchemeName.Length + 1)..];
            var claims = new List<Claim>();
            foreach (var part in payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var idx = part.IndexOf('=');
                if (idx <= 0) continue;
                var key = part[..idx];
                var val = part[(idx + 1)..];
                if (key == "sub") claims.Add(new Claim("sub", val));
                else if (key == "role") claims.Add(new Claim(ClaimTypes.Role, val));
                else if (key == "perm") claims.Add(new Claim("permission", val));
                else if (key == "pwd_change") claims.Add(new Claim(IdentityClaimTypes.PasswordChangeRequired, val));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName)));
        }
    }

    private sealed class QaUserStore :
        IUserRepository,
        IUserQueryReader,
        IIdentityUnitOfWork,
        IAuditLogWriter,
        IOneTimeChallengeRepository,
        IIdentityEmailOutbox,
        IOpaqueTokenService,
        IPasswordPolicy,
        IPasswordService,
        IAuthenticationSessionRepository,
        IReauthenticationVerifier,
        IClock,
        IUserImportFileReader,
        IUserImportTemplateGenerator
    {
        public List<User> Users { get; } = [];
        public List<Role> Roles { get; } = Enum.GetValues<SystemRoleName>()
            .Select(r => new Role { Id = (int)r + 1, RoleName = r.ToString(), Description = r.ToString() })
            .ToList();
        public List<AuditEntry> Audits { get; } = [];
        public List<Guid> RevokedUsers { get; } = [];
        public ReauthenticationOutcome ReauthOutcome { get; set; } = ReauthenticationOutcome.Verified;
        private readonly PasswordPolicy _realPasswordPolicy = new();
        public DateTime UtcNow { get; } = new(2026, 11, 19, 16, 0, 0, DateTimeKind.Utc);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public void Add(AuditEntry entry) => Audits.Add(entry);

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail));
        public Task<User?> GetByUserCodeAsync(string userCode, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(u => u.UserCode == userCode));
        public Task<User?> GetByIdentifierAsync(string normalizedEmail, string normalizedUserCode, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail || u.UserCode == normalizedUserCode));
        public Task<bool> ExistsByEmailOrCodeAsync(string normalizedEmail, string userCode, CancellationToken ct) => Task.FromResult(Users.Any(u => u.NormalizedEmail == normalizedEmail || u.UserCode == userCode));
        public Task<bool> ExistsByUserCodeAsync(string userCode, Guid? excludeUserId, CancellationToken ct) => Task.FromResult(Users.Any(u => u.UserCode == userCode && (!excludeUserId.HasValue || u.Id != excludeUserId.Value)));
        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken ct) => Task.FromResult(Users.Any(u => u.NormalizedEmail == normalizedEmail && (!excludeUserId.HasValue || u.Id != excludeUserId.Value)));
        public Task<IReadOnlySet<string>> GetExistingNormalizedEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(Users.Where(u => emails.Contains(u.NormalizedEmail)).Select(u => u.NormalizedEmail).ToHashSet());
        public Task<IReadOnlySet<string>> GetExistingUserCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(Users.Where(u => u.UserCode != null && codes.Contains(u.UserCode)).Select(u => u.UserCode!).ToHashSet());
        public Task AddToRoleAsync(User user, SystemRoleName role, DateTime assignedAt, CancellationToken ct) => AddToRolesByNameAsync(user, [role.ToString()], assignedAt, ct);
        public async Task AddRangeToRoleAsync(IReadOnlyCollection<User> users, SystemRoleName role, DateTime assignedAt, CancellationToken ct) { foreach (var u in users) { Users.Add(u); await AddToRoleAsync(u, role, assignedAt, ct); } }
        public Task UpdateRolesAsync(User user, IEnumerable<SystemRoleName> roles, DateTime assignedAt, CancellationToken ct) => UpdateRolesByNameAsync(user, roles.Select(r => r.ToString()).ToList(), assignedAt, ct);
        public Task<IReadOnlyList<Role>> GetRolesByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken ct) => Task.FromResult<IReadOnlyList<Role>>(Roles.Where(r => roleNames.Contains(r.RoleName, StringComparer.OrdinalIgnoreCase)).ToList());
        public Task AddToRolesByNameAsync(User user, IReadOnlyCollection<string> roleNames, DateTime assignedAt, CancellationToken ct)
        {
            foreach (var name in roleNames)
            {
                var role = Roles.First(r => string.Equals(r.RoleName, name, StringComparison.OrdinalIgnoreCase));
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role, User = user, AssignedAt = assignedAt });
            }
            return Task.CompletedTask;
        }
        public Task UpdateRolesByNameAsync(User user, IReadOnlyCollection<string> roleNames, DateTime assignedAt, CancellationToken ct) { user.UserRoles.Clear(); return AddToRolesByNameAsync(user, roleNames, assignedAt, ct); }
        public Task AcquireAccountProvisioningLockAsync(CancellationToken ct) => Task.CompletedTask;
        public Task AcquireAdminMutationLockAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken ct) => Task.FromResult(Users.Count(u => u.Status == AccountStatus.Active && (!excludeUserId.HasValue || u.Id != excludeUserId.Value) && u.UserRoles.Any(ur => ur.Role.RoleName == nameof(SystemRoleName.Admin))));
        public Task<bool> AnyAdminExistsAsync(CancellationToken ct) => Task.FromResult(Users.Any(u => u.UserRoles.Any(ur => ur.Role.RoleName == nameof(SystemRoleName.Admin))));
        public void Add(User user) => Users.Add(user);

        public Task<PagedResult<UserSummaryDto>> GetUsersAsync(GetUsersQuery query, CancellationToken ct)
        {
            var q = Users.AsEnumerable();
            if (string.Equals(query.AccountGroup, nameof(UserAccountGroup.Student), StringComparison.OrdinalIgnoreCase))
                q = q.Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == nameof(SystemRoleName.Student)));
            else if (string.Equals(query.AccountGroup, nameof(UserAccountGroup.Staff), StringComparison.OrdinalIgnoreCase))
                q = q.Where(u => !u.UserRoles.Any(ur => ur.Role.RoleName == nameof(SystemRoleName.Student)));
            if (!string.IsNullOrWhiteSpace(query.Role))
                q = q.Where(u => u.UserRoles.Any(ur => string.Equals(ur.Role.RoleName, query.Role, StringComparison.OrdinalIgnoreCase)));
            var items = q.Select(u => new UserSummaryDto(u.Id, u.Email, u.FullName, u.UserCode, u.PhoneNumber, u.Status.ToString(), u.UserRoles.Select(ur => ur.Role.RoleName).ToList(), u.CreatedAt, u.LastLoginAt, u.MustChangePassword)).ToList();
            return Task.FromResult(new PagedResult<UserSummaryDto>(items, items.Count, query.PageNumber, query.PageSize));
        }
        public Task<string?> ResolveRoleNameAsync(string roleName, CancellationToken ct)
            => Task.FromResult(long.TryParse(roleName.Trim(), out _) ? null : Roles.FirstOrDefault(r => string.Equals(r.RoleName, roleName.Trim(), StringComparison.OrdinalIgnoreCase))?.RoleName);

        public Task<OneTimeChallenge?> GetAsync(string selector, ChallengePurpose purpose, CancellationToken ct) => Task.FromResult<OneTimeChallenge?>(null);
        public Task<OneTimeChallenge?> GetForUpdateAsync(string selector, ChallengePurpose purpose, CancellationToken ct) => Task.FromResult<OneTimeChallenge?>(null);
        public Task InvalidateOutstandingAsync(Guid userId, ChallengePurpose purpose, DateTime now, CancellationToken ct) => Task.CompletedTask;
        public void Add(OneTimeChallenge challenge) { }
        public void EnqueueActivationEmail(Guid userId, string to, string fullName, string rawToken, DateTime now) { }
        public void EnqueuePasswordRecoveryEmail(Guid userId, string email, string fullName, string code, DateTime createdAt) { }
        public void EnqueueAdminPasswordResetAlertEmail(Guid userId, string email, string fullName, bool mustChangePassword, DateTime createdAt) { }
        public void AddSession(AuthenticationSession session) { }
        public void AddRefreshToken(RefreshToken refreshToken) { }
        public Task<AuthenticationSession?> GetSessionAsync(Guid sessionId, CancellationToken ct) => Task.FromResult<AuthenticationSession?>(null);
        public Task<RefreshToken?> GetRefreshTokenForUpdateAsync(string selector, CancellationToken ct) => Task.FromResult<RefreshToken?>(null);
        public Task<IReadOnlyList<AuthenticationSession>> GetActiveSessionsAsync(Guid userId, DateTime now, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuthenticationSession>>([]);
        public Task<bool> RevokeActiveSessionAsync(Guid sessionId, Guid userId, SessionRevocationReason reason, DateTime now, CancellationToken ct) => Task.FromResult(false);
        public Task<int> RevokeActiveSessionsAsync(Guid userId, SessionRevocationReason reason, DateTime now, CancellationToken ct) => Task.FromResult(0);
        public Task RevokeAllAsync(Guid userId, SessionRevocationReason reason, DateTime now, CancellationToken ct) { RevokedUsers.Add(userId); return Task.CompletedTask; }
        public Task<ReauthenticationOutcome> VerifyPasswordAsync(Guid userId, string password, CancellationToken ct = default) => Task.FromResult(ReauthOutcome);
        public OpaqueToken CreateToken() => new("sel.val", "sel", [1, 2]);
        public NumericCode CreateNumericCode(int digits) => new("sel", "123456", [1, 2]);
        public bool TryParse(string rawToken, out string selector, out string validator) { selector = "sel"; validator = "val"; return true; }
        public bool Verify(string suppliedSecret, byte[] expectedHash) => true;
        public PasswordPolicyResult Validate(string password) => _realPasswordPolicy.Validate(password);
        public string Hash(User user, string password) => "hash:" + password;
        public bool Verify(User user, string passwordHash, string providedPassword) => passwordHash == "hash:" + providedPassword;
        public ImportFileReadResult Read(Stream stream) => new([], []);
        public byte[] Generate(SystemRoleName role) => [];
    }
}
