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
using UGTM.Modules.Identity.Application.Permissions.GetPermissions;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Roles.CreateRole;
using UGTM.Modules.Identity.Application.Roles.GetRolePermissions;
using UGTM.Modules.Identity.Application.Roles.GetRoles;
using UGTM.Modules.Identity.Application.Roles.UpdateRolePermissions;
using UGTM.Modules.Identity.Application.Roles.UpdateRoleVisibility;
using UGTM.Modules.Identity.Application.Roles.UpdateRoleDescription;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Application.Users.CreateUser;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Application.Users.GetUsers;
using UGTM.Modules.Identity.Application.Users.ImportUsers;
using UGTM.Modules.Identity.Application.Users.UpdateUser;
using UGTM.Modules.Identity.Application.Users.UpdateUserStatus;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.Identity.Infrastructure.Endpoints;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class RoleManagementEndpointsQaTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly QaIdentityStore _store = new();

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
        builder.Services.AddSingleton<IRolePermissionRepository>(_store);
        builder.Services.AddSingleton<IUserRepository>(_store);
        builder.Services.AddSingleton<IUserQueryReader>(_store);
        builder.Services.AddSingleton<IIdentityUnitOfWork>(_store);
        builder.Services.AddSingleton<IAuditLogWriter>(_store);
        builder.Services.AddSingleton<IOneTimeChallengeRepository>(_store);
        builder.Services.AddSingleton<IIdentityEmailOutbox>(_store);
        builder.Services.AddSingleton<IOpaqueTokenService>(_store);
        builder.Services.AddSingleton<IPasswordPolicy>(_store);
        builder.Services.AddSingleton<IPasswordService>(_store);
        builder.Services.AddSingleton<IClock>(_store);
        builder.Services.AddSingleton(new IdentitySecurityPolicy());
        builder.Services.AddSingleton<IUserImportFileReader>(_store);
        builder.Services.AddSingleton<IUserImportTemplateGenerator>(_store);

        builder.Services.AddScoped<GetPermissionsHandler>();
        builder.Services.AddScoped<GetRolesHandler>();
        builder.Services.AddScoped<CreateRoleHandler>();
        builder.Services.AddScoped<GetRolePermissionsHandler>();
        builder.Services.AddScoped<UpdateRolePermissionsHandler>();
        builder.Services.AddScoped<UpdateRoleVisibilityHandler>();
        builder.Services.AddScoped<UpdateRoleDescriptionHandler>();
        builder.Services.AddScoped<GetUsersHandler>();
        builder.Services.AddScoped<CreateUserHandler>();
        builder.Services.AddScoped<UpdateUserHandler>();
        builder.Services.AddScoped<ImportUsersHandler>();
        builder.Services.AddScoped<UpdateUserStatusHandler>();

        builder.Services.AddAuthentication(QaAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, QaAuthHandler>(QaAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapIdentityRolePermissionEndpoints();
        _app.MapIdentityUserEndpoints();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    #region Helpers

    private void Authenticate(Guid userId, string[] roles, params string[] permissions)
    {
        var claims = new List<string> { $"sub={userId}" };
        claims.AddRange(roles.Select(r => $"role={r}"));
        claims.AddRange(permissions.Select(p => $"perm={p}"));
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(QaAuthHandler.SchemeName, string.Join(";", claims));
    }

    private void AuthenticateAsAdmin()
        => Authenticate(Guid.NewGuid(), ["Admin"]);

    private void ClearAuth()
        => _client.DefaultRequestHeaders.Authorization = null;

    private async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("success").GetBoolean(), $"Expected success response, got: {json}");
        return json.GetProperty("data");
    }

    private async Task<(string Code, string Message)> ReadErrorAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.Object)
        {
            return (
                errorProp.GetProperty("code").GetString() ?? string.Empty,
                errorProp.GetProperty("message").GetString() ?? string.Empty);
        }

        return (string.Empty, text);
    }

    private async Task<int> CreateCustomRoleAsync(string roleName, string? description = null, int[]? permissionIds = null)
    {
        var response = await _client.PostAsJsonAsync("/api/roles", new
        {
            roleName,
            description,
            permissionIds
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        return data.GetProperty("roleId").GetInt32();
    }

    #endregion

    #region A. AuthN & AuthZ (RBAC + PBAC)

    [Theory]
    [InlineData("GET", "/api/roles")]
    [InlineData("POST", "/api/roles")]
    [InlineData("GET", "/api/roles/1/permissions")]
    [InlineData("PUT", "/api/roles/4/permissions")]
    [InlineData("PUT", "/api/roles/4/description")]
    [InlineData("PATCH", "/api/roles/7/hide")]
    [InlineData("PATCH", "/api/roles/7/unhide")]
    [InlineData("PATCH", "/api/roles/7/visibility")]
    public async Task RoleEndpoints_WhenUnauthenticated_Return401Unauthorized(string method, string path)
    {
        ClearAuth();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/roles", "Student")]
    [InlineData("POST", "/api/roles", "Lecturer")]
    [InlineData("GET", "/api/roles/4/permissions", "Faculty")]
    [InlineData("PUT", "/api/roles/4/permissions", "AcademicAffairs")]
    [InlineData("PUT", "/api/roles/4/description", "AcademicAffairs")]
    [InlineData("PATCH", "/api/roles/7/hide", "Lecturer")]
    [InlineData("PATCH", "/api/roles/7/unhide", "Faculty")]
    [InlineData("PATCH", "/api/roles/7/visibility", "Student")]
    public async Task RoleEndpoints_WithoutRolesManagePermission_Return403Forbidden(string method, string path, string role)
    {
        Authenticate(Guid.NewGuid(), [role], PermissionCodes.IdentityUsersRead, PermissionCodes.IdentityPermissionsRead);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RoleEndpoints_NonAdminWithExplicitPermissionClaim_Succeeds()
    {
        Authenticate(Guid.NewGuid(), ["Faculty"], PermissionCodes.IdentityRolesManage);

        var response = await _client.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.Equal(6, data.GetArrayLength());
    }

    [Fact]
    public async Task UpdateDescription_returns_updated_value_and_rejects_invalid_length()
    {
        AuthenticateAsAdmin();
        var updated = await _client.PutAsJsonAsync("/api/roles/4/description", new { description = "  Mô tả mới  " });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Mô tả mới", (await ReadDataAsync(updated)).GetProperty("description").GetString());
        var invalid = await _client.PutAsJsonAsync("/api/roles/4/description", new { description = new string('x', 256) });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var missing = await _client.PutAsJsonAsync("/api/roles/99999/description", new { description = "test" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task RoleEndpoints_AdminWithoutExplicitPermissionClaim_BypassSucceeds()
    {
        AuthenticateAsAdmin();

        var response = await _client.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region B. Happy Path & Full E2E Lifecycle

    [Fact]
    public async Task FullRoleLifecycle_Create_List_AssignToUser_PreventHideWhenInUse_Unassign_Hide_BlockOperations_Unhide()
    {
        Authenticate(Guid.NewGuid(), ["Admin"], PermissionCodes.IdentityUsersRead, PermissionCodes.IdentityUsersManage);

        // 1. Initial GET /api/roles returns 6 system roles
        var initialListRes = await _client.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.OK, initialListRes.StatusCode);
        var initialRoles = await ReadDataAsync(initialListRes);
        Assert.Equal(6, initialRoles.GetArrayLength());
        foreach (var r in initialRoles.EnumerateArray())
        {
            Assert.True(r.GetProperty("isSystemRole").GetBoolean());
            Assert.False(r.GetProperty("isHidden").GetBoolean());
            Assert.False(r.GetProperty("canHide").GetBoolean());
        }

        // 2. Create custom role with Vietnamese Unicode, whitespace trim, duplicate permission IDs
        var createRes = await _client.PostAsJsonAsync("/api/roles", new
        {
            roleName = "  Trưởng Bộ Môn_KLTN  ",
            description = "  Phụ trách chuyên môn bộ môn  ",
            permissionIds = new[] { 2, 1, 2 }
        });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await ReadDataAsync(createRes);
        var customRoleId = created.GetProperty("roleId").GetInt32();
        Assert.True(customRoleId > 6);
        Assert.Equal("Trưởng Bộ Môn_KLTN", created.GetProperty("roleName").GetString());
        Assert.Equal("Phụ trách chuyên môn bộ môn", created.GetProperty("description").GetString());
        Assert.False(created.GetProperty("isSystemRole").GetBoolean());
        Assert.False(created.GetProperty("isHidden").GetBoolean());
        Assert.Equal(0, created.GetProperty("userCount").GetInt32());
        Assert.Equal(2, created.GetProperty("permissions").GetArrayLength());

        // 3. GET /api/roles lists custom role after 6 system roles with canHide = true
        var listAfterCreate = await ReadDataAsync(await _client.GetAsync("/api/roles"));
        Assert.Equal(7, listAfterCreate.GetArrayLength());
        var customSummary = listAfterCreate.EnumerateArray().Last();
        Assert.Equal(customRoleId, customSummary.GetProperty("roleId").GetInt32());
        Assert.Equal(2, customSummary.GetProperty("permissionCount").GetInt32());
        Assert.Equal(0, customSummary.GetProperty("userCount").GetInt32());
        Assert.True(customSummary.GetProperty("canHide").GetBoolean());

        // 4. Assign custom role to a new user (case-insensitive match)
        var createUserRes = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "head.cs@ugtm.edu.vn",
            fullName = "TS. Nguyễn Văn Trưởng",
            userCode = "GV900",
            roles = new[] { "Lecturer", "trưởng bộ môn_kltn" },
            sendActivationEmail = false
        });
        Assert.Equal(HttpStatusCode.OK, createUserRes.StatusCode);
        var createdUser = await ReadDataAsync(createUserRes);
        var userId = createdUser.GetProperty("id").GetGuid();
        Assert.Contains("Trưởng Bộ Môn_KLTN", createdUser.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));

        // 5. Filter GET /api/users?role=... by custom role
        var usersByRoleRes = await _client.GetAsync("/api/users?role=" + Uri.EscapeDataString("TRƯỞNG BỘ MÔN_KLTN"));
        Assert.Equal(HttpStatusCode.OK, usersByRoleRes.StatusCode);
        var usersPage = await ReadDataAsync(usersByRoleRes);
        Assert.Equal(1, usersPage.GetProperty("totalCount").GetInt32());

        // 6. Verify userCount = 1 and canHide = false; hiding must return 409 Identity.RoleInUse
        var listInUse = await ReadDataAsync(await _client.GetAsync("/api/roles"));
        var inUseEntry = listInUse.EnumerateArray().First(r => r.GetProperty("roleId").GetInt32() == customRoleId);
        Assert.Equal(1, inUseEntry.GetProperty("userCount").GetInt32());
        Assert.False(inUseEntry.GetProperty("canHide").GetBoolean());

        var hideInUseRes = await _client.PatchAsync($"/api/roles/{customRoleId}/hide", null);
        Assert.Equal(HttpStatusCode.Conflict, hideInUseRes.StatusCode);
        Assert.Equal("Identity.RoleInUse", (await ReadErrorAsync(hideInUseRes)).Code);

        // 7. Remove custom role from user via PUT /api/users/{id}
        var unassignRes = await _client.PutAsJsonAsync($"/api/users/{userId}", new
        {
            fullName = "TS. Nguyễn Văn Trưởng",
            userCode = "GV900",
            roles = new[] { "Lecturer" }
        });
        Assert.Equal(HttpStatusCode.OK, unassignRes.StatusCode);

        // 8. Hide unused custom role via PATCH /api/roles/{id}/hide
        var hideRes = await _client.PatchAsync($"/api/roles/{customRoleId}/hide", null);
        Assert.Equal(HttpStatusCode.OK, hideRes.StatusCode);
        var hiddenSummary = await ReadDataAsync(hideRes);
        Assert.True(hiddenSummary.GetProperty("isHidden").GetBoolean());
        Assert.False(hiddenSummary.GetProperty("canHide").GetBoolean());
        Assert.Equal(AuditActions.RoleHidden, _store.AuditEntries.Last().Action);

        // 9. Default GET /api/roles excludes hidden role; includeHidden=true shows it
        var defaultVisible = await ReadDataAsync(await _client.GetAsync("/api/roles"));
        Assert.Equal(6, defaultVisible.GetArrayLength());

        var allIncludingHidden = await ReadDataAsync(await _client.GetAsync("/api/roles?includeHidden=true"));
        Assert.Equal(7, allIncludingHidden.GetArrayLength());
        Assert.Contains(allIncludingHidden.EnumerateArray(),
            r => r.GetProperty("roleId").GetInt32() == customRoleId && r.GetProperty("isHidden").GetBoolean());

        // 10. While hidden: block permissions edit, user assignment, duplicate name creation, and duplicate hide
        var editPermWhileHidden = await _client.PutAsJsonAsync($"/api/roles/{customRoleId}/permissions", new { permissionIds = new[] { 1 } });
        Assert.Equal(HttpStatusCode.BadRequest, editPermWhileHidden.StatusCode);
        Assert.Equal("Identity.RoleIsHidden", (await ReadErrorAsync(editPermWhileHidden)).Code);

        var assignNewUserWhileHidden = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "new2@ugtm.edu.vn",
            fullName = "New User",
            roles = new[] { "Trưởng Bộ Môn_KLTN" },
            sendActivationEmail = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, assignNewUserWhileHidden.StatusCode);
        Assert.Equal("Identity.RoleIsHidden", (await ReadErrorAsync(assignNewUserWhileHidden)).Code);

        var assignExistingUserWhileHidden = await _client.PutAsJsonAsync($"/api/users/{userId}", new
        {
            fullName = "TS. Nguyễn Văn Trưởng",
            userCode = "GV900",
            roles = new[] { "Lecturer", "Trưởng Bộ Môn_KLTN" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, assignExistingUserWhileHidden.StatusCode);
        Assert.Equal("Identity.RoleIsHidden", (await ReadErrorAsync(assignExistingUserWhileHidden)).Code);

        var createDuplicateOfHidden = await _client.PostAsJsonAsync("/api/roles", new
        {
            roleName = "trưởng bộ môn_kltn",
            description = "Duplicate"
        });
        Assert.Equal(HttpStatusCode.Conflict, createDuplicateOfHidden.StatusCode);
        Assert.Equal("Identity.RoleAlreadyExists", (await ReadErrorAsync(createDuplicateOfHidden)).Code);

        var hideAgainRes = await _client.PatchAsync($"/api/roles/{customRoleId}/hide", null);
        Assert.Equal(HttpStatusCode.Conflict, hideAgainRes.StatusCode);
        Assert.Equal("Identity.RoleAlreadyHidden", (await ReadErrorAsync(hideAgainRes)).Code);

        // 11. Unhide role via PATCH /api/roles/{id}/unhide
        var unhideRes = await _client.PatchAsync($"/api/roles/{customRoleId}/unhide", null);
        Assert.Equal(HttpStatusCode.OK, unhideRes.StatusCode);
        var unhiddenSummary = await ReadDataAsync(unhideRes);
        Assert.False(unhiddenSummary.GetProperty("isHidden").GetBoolean());
        Assert.True(unhiddenSummary.GetProperty("canHide").GetBoolean());
        Assert.Equal(AuditActions.RoleUnhidden, _store.AuditEntries.Last().Action);

        // 12. Toggle via PATCH /api/roles/{id}/visibility { isHidden: true } and { isHidden: false }
        var visHide = await _client.PatchAsJsonAsync($"/api/roles/{customRoleId}/visibility", new { isHidden = true });
        Assert.Equal(HttpStatusCode.OK, visHide.StatusCode);
        Assert.True((await ReadDataAsync(visHide)).GetProperty("isHidden").GetBoolean());

        var visShow = await _client.PatchAsJsonAsync($"/api/roles/{customRoleId}/visibility", new { isHidden = false });
        Assert.Equal(HttpStatusCode.OK, visShow.StatusCode);
        Assert.False((await ReadDataAsync(visShow)).GetProperty("isHidden").GetBoolean());
    }

    #endregion

    #region C. System Role Protection & In-Use State Rules

    [Theory]
    [InlineData(1)] // Admin
    [InlineData(2)] // Faculty
    [InlineData(3)] // AcademicAffairs
    [InlineData(4)] // Lecturer
    [InlineData(5)] // Student
    [InlineData(6)] // CouncilMember
    public async Task HideSystemRoles_ViaHideAndVisibilityEndpoints_Returns400CannotHideSystemRole(int systemRoleId)
    {
        AuthenticateAsAdmin();

        var hideRes = await _client.PatchAsync($"/api/roles/{systemRoleId}/hide", null);
        Assert.Equal(HttpStatusCode.BadRequest, hideRes.StatusCode);
        Assert.Equal("Identity.CannotHideSystemRole", (await ReadErrorAsync(hideRes)).Code);

        var visRes = await _client.PatchAsJsonAsync($"/api/roles/{systemRoleId}/visibility", new { isHidden = true });
        Assert.Equal(HttpStatusCode.BadRequest, visRes.StatusCode);
        Assert.Equal("Identity.CannotHideSystemRole", (await ReadErrorAsync(visRes)).Code);
    }

    [Theory]
    [InlineData(AccountStatus.PendingActivation)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Disabled)]
    public async Task HideRole_AssignedToNonActiveUser_StillReturns409RoleInUse(AccountStatus userStatus)
    {
        AuthenticateAsAdmin();
        var roleId = await CreateCustomRoleAsync($"CustomFor_{userStatus}");
        var role = _store.Roles.First(r => r.Id == roleId);

        var user = User.Create(
            Guid.NewGuid(),
            $"{userStatus.ToString().ToLowerInvariant()}@ugtm.edu.vn",
            $"{userStatus.ToString().ToUpperInvariant()}@UGTM.EDU.VN",
            "Test User",
            null,
            null,
            null,
            userStatus,
            _store.UtcNow);
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role, User = user, AssignedAt = _store.UtcNow });
        _store.Users.Add(user);

        var hideRes = await _client.PatchAsync($"/api/roles/{roleId}/hide", null);
        Assert.Equal(HttpStatusCode.Conflict, hideRes.StatusCode);
        Assert.Equal("Identity.RoleInUse", (await ReadErrorAsync(hideRes)).Code);
    }

    [Fact]
    public async Task UnhideVisibleRole_Returns409RoleNotHidden()
    {
        AuthenticateAsAdmin();
        var roleId = await CreateCustomRoleAsync("VisibleCustom");

        var unhideCustom = await _client.PatchAsync($"/api/roles/{roleId}/unhide", null);
        Assert.Equal(HttpStatusCode.Conflict, unhideCustom.StatusCode);
        Assert.Equal("Identity.RoleNotHidden", (await ReadErrorAsync(unhideCustom)).Code);

        var unhideSystem = await _client.PatchAsync("/api/roles/1/unhide", null);
        Assert.Equal(HttpStatusCode.Conflict, unhideSystem.StatusCode);
        Assert.Equal("Identity.RoleNotHidden", (await ReadErrorAsync(unhideSystem)).Code);
    }

    #endregion

    #region D. Type Mismatch & Malformed JSON

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("yes")]
    public async Task GetRoles_WithInvalidBooleanQueryString_Returns400BadRequestNot500(string invalidBool)
    {
        AuthenticateAsAdmin();

        var response = await _client.GetAsync($"/api/roles?includeHidden={invalidBool}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RoleEndpoints_WithCompletelyEmptyJsonBodyOrUnsupportedMediaType_ReturnClientErrorNot500()
    {
        AuthenticateAsAdmin();

        using var emptyJson = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        var emptyBodyRes = await _client.PostAsync("/api/roles", emptyJson);
        Assert.Equal(HttpStatusCode.BadRequest, emptyBodyRes.StatusCode);

        using var wrongMedia = new StringContent("""{"roleName":"PlainTextRole"}""", Encoding.UTF8, "text/plain");
        var wrongMediaRes = await _client.PostAsync("/api/roles", wrongMedia);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongMediaRes.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/roles/abc/permissions")]
    [InlineData("PUT", "/api/roles/abc/permissions")]
    [InlineData("PATCH", "/api/roles/abc/hide")]
    [InlineData("PATCH", "/api/roles/abc/unhide")]
    [InlineData("PATCH", "/api/roles/abc/visibility")]
    [InlineData("PATCH", "/api/roles/99999999999999999999/hide")]
    public async Task RoleEndpoints_WithNonIntegerOrOverflowRouteId_Return404NotFound(string method, string path)
    {
        AuthenticateAsAdmin();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"roleName": "Test" """)] // truncated JSON
    [InlineData("""{"roleName": 12345}""")] // numeric instead of string
    [InlineData("""{"roleName": true}""")] // bool instead of string
    [InlineData("""{"roleName": ["ArrayRole"]}""")] // array instead of string
    [InlineData("""{"roleName": "ValidRole", "permissionIds": ["abc"]}""")] // string element in int[]
    [InlineData("""{"roleName": "ValidRole", "permissionIds": "1"}""")] // string instead of int[]
    public async Task CreateRole_WithMalformedOrTypeMismatchedJson_Returns400BadRequestNot500(string rawJson)
    {
        AuthenticateAsAdmin();
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/roles", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""{bad json""")]
    [InlineData("""{"isHidden": "true"}""")]
    [InlineData("""{"isHidden": "yes"}""")]
    [InlineData("""{"isHidden": 1}""")]
    public async Task UpdateRoleVisibility_WithMalformedOrTypeMismatchedJson_Returns400BadRequestNot500(string rawJson)
    {
        AuthenticateAsAdmin();
        var roleId = await CreateCustomRoleAsync("VisibilityTypeTest");
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await _client.PatchAsync($"/api/roles/{roleId}/visibility", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""{invalid""")]
    [InlineData("""{"permissionIds": ["not-an-int"]}""")]
    [InlineData("""{"permissionIds": 1}""")]
    public async Task UpdateRolePermissions_WithMalformedOrTypeMismatchedJson_Returns400BadRequestNot500(string rawJson)
    {
        AuthenticateAsAdmin();
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await _client.PutAsync("/api/roles/4/permissions", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region E. Empty, Missing & Null Payloads

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"roleName": null}""")]
    [InlineData("""{"roleName": ""}""")]
    [InlineData("""{"roleName": "    "}""")]
    [InlineData("null")]
    public async Task CreateRole_WithEmptyOrNullRoleName_Returns400InvalidRoleName(string rawJson)
    {
        AuthenticateAsAdmin();
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/roles", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Identity.InvalidRoleName", (await ReadErrorAsync(response)).Code);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"isHidden": null}""")]
    [InlineData("null")]
    public async Task UpdateRoleVisibility_WithEmptyOrNullIsHidden_Returns400InvalidInput(string rawJson)
    {
        AuthenticateAsAdmin();
        var roleId = await CreateCustomRoleAsync("VisibilityNullTest");
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await _client.PatchAsync($"/api/roles/{roleId}/visibility", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Identity.InvalidInput", (await ReadErrorAsync(response)).Code);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"permissionIds": null}""")]
    [InlineData("null")]
    public async Task UpdateRolePermissions_WithEmptyOrNullPermissionIds_Returns400InvalidPermission(string rawJson)
    {
        AuthenticateAsAdmin();
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await _client.PutAsync("/api/roles/4/permissions", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Identity.InvalidPermission", (await ReadErrorAsync(response)).Code);
    }

    [Theory]
    [InlineData("""{"email":"u1@ugtm.edu.vn","fullName":"U1","roles":[]}""")]
    [InlineData("""{"email":"u2@ugtm.edu.vn","fullName":"U2","roles":[""]}""")]
    [InlineData("""{"email":"u3@ugtm.edu.vn","fullName":"U3","roles":["Lecturer", null]}""")]
    [InlineData("""{"email":"u4@ugtm.edu.vn","fullName":"U4","roles":null}""")]
    public async Task CreateUser_WithEmptyOrNullRoles_Returns400InvalidRole(string rawJson)
    {
        AuthenticateAsAdmin();
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/users", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Identity.InvalidRole", (await ReadErrorAsync(response)).Code);
    }

    #endregion

    #region F. Boundary Values & Control Characters

    [Fact]
    public async Task CreateRole_BoundaryLengths_Exact2And50Succeed_1And51Fail()
    {
        AuthenticateAsAdmin();

        var len1 = await _client.PostAsJsonAsync("/api/roles", new { roleName = "A" });
        Assert.Equal(HttpStatusCode.BadRequest, len1.StatusCode);
        Assert.Equal("Identity.InvalidRoleName", (await ReadErrorAsync(len1)).Code);

        var len2 = await _client.PostAsJsonAsync("/api/roles", new { roleName = "AB" });
        Assert.Equal(HttpStatusCode.OK, len2.StatusCode);

        var len50 = await _client.PostAsJsonAsync("/api/roles", new { roleName = new string('R', 50) });
        Assert.Equal(HttpStatusCode.OK, len50.StatusCode);

        var len51 = await _client.PostAsJsonAsync("/api/roles", new { roleName = new string('R', 51) });
        Assert.Equal(HttpStatusCode.BadRequest, len51.StatusCode);
        Assert.Equal("Identity.InvalidRoleName", (await ReadErrorAsync(len51)).Code);
    }

    [Theory]
    [InlineData("Dept\nHead")]
    [InlineData("Dept\rHead")]
    [InlineData("Dept\tHead")]
    [InlineData("___---")]
    [InlineData("123456")]
    [InlineData("Role<script>")]
    [InlineData("Role;DROP")]
    public async Task CreateRole_WithControlCharsNoLettersNumericOrSpecialSymbols_Returns400InvalidRoleName(string badName)
    {
        AuthenticateAsAdmin();

        var response = await _client.PostAsJsonAsync("/api/roles", new { roleName = badName });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Identity.InvalidRoleName", (await ReadErrorAsync(response)).Code);
    }

    [Fact]
    public async Task CreateRole_DescriptionBoundaries_255Succeeds_256Fails_WhitespaceBecomesNull()
    {
        AuthenticateAsAdmin();

        var desc255 = await _client.PostAsJsonAsync("/api/roles", new { roleName = "RoleDesc255", description = new string('d', 255) });
        Assert.Equal(HttpStatusCode.OK, desc255.StatusCode);

        var desc256 = await _client.PostAsJsonAsync("/api/roles", new { roleName = "RoleDesc256", description = new string('d', 256) });
        Assert.Equal(HttpStatusCode.BadRequest, desc256.StatusCode);
        Assert.Equal("Identity.InvalidInput", (await ReadErrorAsync(desc256)).Code);

        var descWhitespace = await _client.PostAsJsonAsync("/api/roles", new { roleName = "RoleDescBlank", description = "    " });
        Assert.Equal(HttpStatusCode.OK, descWhitespace.StatusCode);
        var data = await ReadDataAsync(descWhitespace);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task CreateRole_MixedValidAndInvalidPermissionIds_RollsBackAtomically()
    {
        AuthenticateAsAdmin();
        var countBefore = _store.Roles.Count;

        var resZero = await _client.PostAsJsonAsync("/api/roles", new { roleName = "RoleZeroPerm", permissionIds = new[] { 1, 0 } });
        Assert.Equal(HttpStatusCode.BadRequest, resZero.StatusCode);
        Assert.Equal("Identity.InvalidPermission", (await ReadErrorAsync(resZero)).Code);

        var resNeg = await _client.PostAsJsonAsync("/api/roles", new { roleName = "RoleNegPerm", permissionIds = new[] { -1 } });
        Assert.Equal(HttpStatusCode.BadRequest, resNeg.StatusCode);

        var resMissing = await _client.PostAsJsonAsync("/api/roles", new { roleName = "RoleMissingPerm", permissionIds = new[] { 1, 99999 } });
        Assert.Equal(HttpStatusCode.BadRequest, resMissing.StatusCode);
        Assert.Equal("Identity.InvalidPermission", (await ReadErrorAsync(resMissing)).Code);

        Assert.Equal(countBefore, _store.Roles.Count);
    }

    #endregion

    #region G. Non-existent References

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(9999)]
    public async Task RoleEndpoints_WithNonExistentOrNonPositiveId_Return404RoleNotFound(int missingRoleId)
    {
        AuthenticateAsAdmin();

        var getRes = await _client.GetAsync($"/api/roles/{missingRoleId}/permissions");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
        Assert.Equal("Identity.RoleNotFound", (await ReadErrorAsync(getRes)).Code);

        var putRes = await _client.PutAsJsonAsync($"/api/roles/{missingRoleId}/permissions", new { permissionIds = new[] { 1 } });
        Assert.Equal(HttpStatusCode.NotFound, putRes.StatusCode);
        Assert.Equal("Identity.RoleNotFound", (await ReadErrorAsync(putRes)).Code);

        var hideRes = await _client.PatchAsync($"/api/roles/{missingRoleId}/hide", null);
        Assert.Equal(HttpStatusCode.NotFound, hideRes.StatusCode);
        Assert.Equal("Identity.RoleNotFound", (await ReadErrorAsync(hideRes)).Code);

        var unhideRes = await _client.PatchAsync($"/api/roles/{missingRoleId}/unhide", null);
        Assert.Equal(HttpStatusCode.NotFound, unhideRes.StatusCode);
        Assert.Equal("Identity.RoleNotFound", (await ReadErrorAsync(unhideRes)).Code);

        var visRes = await _client.PatchAsJsonAsync($"/api/roles/{missingRoleId}/visibility", new { isHidden = true });
        Assert.Equal(HttpStatusCode.NotFound, visRes.StatusCode);
        Assert.Equal("Identity.RoleNotFound", (await ReadErrorAsync(visRes)).Code);
    }

    [Fact]
    public async Task UserEndpoints_WithNonExistentCustomRole_Return400InvalidRole()
    {
        AuthenticateAsAdmin();

        var createUserRes = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "ghostrole@ugtm.edu.vn",
            fullName = "Ghost Role User",
            roles = new[] { "GhostRole" },
            sendActivationEmail = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, createUserRes.StatusCode);
        Assert.Equal("Identity.InvalidRole", (await ReadErrorAsync(createUserRes)).Code);

        var validCreate = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "valid.ref@ugtm.edu.vn",
            fullName = "Valid Ref",
            roles = new[] { "Lecturer" },
            sendActivationEmail = false
        });
        var userId = (await ReadDataAsync(validCreate)).GetProperty("id").GetGuid();

        var updateUserRes = await _client.PutAsJsonAsync($"/api/users/{userId}", new
        {
            fullName = "Valid Ref",
            roles = new[] { "NonExistentCustomRole" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, updateUserRes.StatusCode);
        Assert.Equal("Identity.InvalidRole", (await ReadErrorAsync(updateUserRes)).Code);

        var filterGhost = await _client.GetAsync("/api/users?role=GhostRole");
        Assert.Equal(HttpStatusCode.BadRequest, filterGhost.StatusCode);
        Assert.Equal("Identity.InvalidRole", (await ReadErrorAsync(filterGhost)).Code);

        var filterNumeric = await _client.GetAsync("/api/users?role=123");
        Assert.Equal(HttpStatusCode.BadRequest, filterNumeric.StatusCode);
        Assert.Equal("Identity.InvalidRole", (await ReadErrorAsync(filterNumeric)).Code);
    }

    #endregion

    #region H. Student & Custom Role Conflict Rules

    [Fact]
    public async Task StudentRoleConflict_AppliesToCustomRolesOnCreateAndUpdateUser()
    {
        AuthenticateAsAdmin();
        await CreateCustomRoleAsync("DepartmentHead", "Trưởng Bộ môn");

        var createCombo = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "combo@ugtm.edu.vn",
            fullName = "Combo Student",
            roles = new[] { "Student", "DepartmentHead" },
            sendActivationEmail = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, createCombo.StatusCode);
        Assert.Equal("Identity.StudentRoleConflict", (await ReadErrorAsync(createCombo)).Code);

        var createCustomUser = await _client.PostAsJsonAsync("/api/users", new
        {
            email = "head2@ugtm.edu.vn",
            fullName = "Head Two",
            roles = new[] { "DepartmentHead" },
            sendActivationEmail = false
        });
        Assert.Equal(HttpStatusCode.OK, createCustomUser.StatusCode);
        var userId = (await ReadDataAsync(createCustomUser)).GetProperty("id").GetGuid();

        var convertToStudent = await _client.PutAsJsonAsync($"/api/users/{userId}", new
        {
            fullName = "Head Two",
            roles = new[] { "Student" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, convertToStudent.StatusCode);
        Assert.Equal("Identity.CannotBecomeStudent", (await ReadErrorAsync(convertToStudent)).Code);
    }

    #endregion

    #region QA Test Doubles

    private sealed class QaAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "IdentityQaScheme";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var header = headerValues.ToString();
            if (!header.StartsWith($"{SchemeName} ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

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
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    private sealed class QaIdentityStore :
        IRolePermissionRepository,
        IUserRepository,
        IUserQueryReader,
        IIdentityUnitOfWork,
        IAuditLogWriter,
        IOneTimeChallengeRepository,
        IIdentityEmailOutbox,
        IOpaqueTokenService,
        IPasswordPolicy,
        IPasswordService,
        IClock,
        IUserImportFileReader,
        IUserImportTemplateGenerator
    {
        public List<Permission> Permissions { get; } =
        [
            new Permission { Id = 1, PermissionCode = PermissionCodes.IdentityUsersRead, PermissionName = "Xem users", Module = "Identity" },
            new Permission { Id = 2, PermissionCode = PermissionCodes.IdentityUsersManage, PermissionName = "Quản lý users", Module = "Identity" },
            new Permission { Id = 3, PermissionCode = PermissionCodes.IdentityRolesManage, PermissionName = "Quản lý roles", Module = "Identity" }
        ];

        public List<Role> Roles { get; } = Enum.GetValues<SystemRoleName>()
            .Select(r => new Role { Id = (int)r + 1, RoleName = r.ToString(), Description = r.ToString(), IsHidden = false })
            .ToList();

        public List<RolePermission> RolePermissions { get; } = [];
        public List<User> Users { get; } = [];
        public List<AuditEntry> AuditEntries { get; } = [];

        public DateTime UtcNow { get; } = new(2026, 11, 19, 16, 0, 0, DateTimeKind.Utc);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);

        public void Add(AuditEntry entry) => AuditEntries.Add(entry);

        // IRolePermissionRepository
        public Task<IReadOnlyList<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Permission>>(Permissions);

        public Task<IReadOnlyList<RoleWithCounts>> GetAllRolesWithCountsAsync(bool includeHidden, CancellationToken cancellationToken)
        {
            var query = includeHidden ? Roles : Roles.Where(r => !r.IsHidden);
            var list = query
                .OrderBy(r => r.Id)
                .Select(r => new RoleWithCounts(
                    r,
                    Users.Count(u => u.UserRoles.Any(ur => ur.RoleId == r.Id)),
                    RolePermissions.Count(rp => rp.RoleId == r.Id)))
                .ToList();
            return Task.FromResult<IReadOnlyList<RoleWithCounts>>(list);
        }

        public Task<Role?> GetRoleWithPermissionsAsync(int roleId, CancellationToken cancellationToken)
        {
            var role = Roles.FirstOrDefault(r => r.Id == roleId);
            if (role != null)
            {
                role.RolePermissions = RolePermissions
                    .Where(rp => rp.RoleId == roleId)
                    .Select(rp => new RolePermission
                    {
                        RoleId = rp.RoleId,
                        PermissionId = rp.PermissionId,
                        Permission = Permissions.First(p => p.Id == rp.PermissionId)
                    })
                    .ToList();
            }
            return Task.FromResult(role);
        }

        public Task<Role?> GetRoleByIdForUpdateAsync(int roleId, CancellationToken cancellationToken)
            => Task.FromResult(Roles.FirstOrDefault(r => r.Id == roleId));

        public Task<int> GetRoleUserCountAsync(int roleId, CancellationToken cancellationToken)
            => Task.FromResult(Users.Count(u => u.UserRoles.Any(ur => ur.RoleId == roleId)));

        public Task<int> GetRolePermissionCountAsync(int roleId, CancellationToken cancellationToken)
            => Task.FromResult(RolePermissions.Count(rp => rp.RoleId == roleId));

        public Task<bool> RoleExistsAsync(int roleId, CancellationToken cancellationToken)
            => Task.FromResult(Roles.Any(r => r.Id == roleId));

        public Task<bool> RoleNameExistsAsync(string roleName, CancellationToken cancellationToken)
            => Task.FromResult(Roles.Any(r => string.Equals(r.RoleName, roleName.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<Permission>> GetPermissionsByIdsAsync(IEnumerable<int> permissionIds, CancellationToken cancellationToken)
        {
            var set = permissionIds.ToHashSet();
            return Task.FromResult<IReadOnlyList<Permission>>(Permissions.Where(p => set.Contains(p.Id)).ToList());
        }

        public Task<Role> CreateRoleAsync(string roleName, string? description, IEnumerable<int> permissionIds, DateTime assignedAt, CancellationToken cancellationToken)
        {
            var role = new Role
            {
                Id = Roles.Count > 0 ? Roles.Max(r => r.Id) + 1 : 1,
                RoleName = roleName.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                IsHidden = false
            };
            Roles.Add(role);
            foreach (var pid in permissionIds.Distinct())
            {
                RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid, AssignedAt = assignedAt, Role = role });
            }
            return Task.FromResult(role);
        }

        public Task UpdateRolePermissionsAsync(int roleId, IEnumerable<int> permissionIds, DateTime assignedAt, CancellationToken cancellationToken)
        {
            RolePermissions.RemoveAll(rp => rp.RoleId == roleId);
            foreach (var pid in permissionIds.Distinct())
            {
                RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = pid, AssignedAt = assignedAt });
            }
            return Task.CompletedTask;
        }

        public Task RotateSecurityStampForRoleUsersAsync(int roleId, CancellationToken cancellationToken)
        {
            foreach (var user in Users.Where(u => u.UserRoles.Any(ur => ur.RoleId == roleId)))
            {
                user.RotateSecurityStamp();
            }
            return Task.CompletedTask;
        }

        // IUserRepository
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail));

        public Task<User?> GetByUserCodeAsync(string userCode, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.UserCode == userCode));

        public Task<User?> GetByIdentifierAsync(string normalizedEmail, string normalizedUserCode, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail || u.UserCode == normalizedUserCode));

        public Task<bool> ExistsByEmailOrCodeAsync(string normalizedEmail, string userCode, CancellationToken cancellationToken)
            => Task.FromResult(Users.Any(u => u.NormalizedEmail == normalizedEmail || u.UserCode == userCode));

        public Task<bool> ExistsByUserCodeAsync(string userCode, Guid? excludeUserId, CancellationToken cancellationToken)
            => Task.FromResult(Users.Any(u => u.UserCode == userCode && (!excludeUserId.HasValue || u.Id != excludeUserId.Value)));

        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken)
            => Task.FromResult(Users.Any(u => u.NormalizedEmail == normalizedEmail && (!excludeUserId.HasValue || u.Id != excludeUserId.Value)));

        public Task<IReadOnlySet<string>> GetExistingNormalizedEmailsAsync(IReadOnlyCollection<string> normalizedEmails, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlySet<string>>(Users.Where(u => normalizedEmails.Contains(u.NormalizedEmail)).Select(u => u.NormalizedEmail).ToHashSet(StringComparer.OrdinalIgnoreCase));

        public Task<IReadOnlySet<string>> GetExistingUserCodesAsync(IReadOnlyCollection<string> userCodes, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlySet<string>>(Users.Where(u => u.UserCode is not null && userCodes.Contains(u.UserCode)).Select(u => u.UserCode!).ToHashSet(StringComparer.OrdinalIgnoreCase));

        public Task AddToRoleAsync(User user, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken)
            => AddToRolesByNameAsync(user, [role.ToString()], assignedAt, cancellationToken);

        public async Task AddRangeToRoleAsync(IReadOnlyCollection<User> users, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken)
        {
            foreach (var u in users)
            {
                Users.Add(u);
                await AddToRoleAsync(u, role, assignedAt, cancellationToken);
            }
        }

        public Task UpdateRolesAsync(User user, IEnumerable<SystemRoleName> roles, DateTime assignedAt, CancellationToken cancellationToken)
            => UpdateRolesByNameAsync(user, roles.Select(r => r.ToString()).ToList(), assignedAt, cancellationToken);

        public Task<IReadOnlyList<Role>> GetRolesByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken)
        {
            var requested = roleNames.Select(n => n.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlyList<Role>>(Roles.Where(r => requested.Contains(r.RoleName)).ToList());
        }

        public Task AddToRolesByNameAsync(User user, IReadOnlyCollection<string> roleNames, DateTime assignedAt, CancellationToken cancellationToken)
        {
            foreach (var name in roleNames)
            {
                var role = Roles.First(r => string.Equals(r.RoleName, name, StringComparison.OrdinalIgnoreCase));
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role, User = user, AssignedAt = assignedAt });
            }
            return Task.CompletedTask;
        }

        public Task UpdateRolesByNameAsync(User user, IReadOnlyCollection<string> roleNames, DateTime assignedAt, CancellationToken cancellationToken)
        {
            user.UserRoles.Clear();
            return AddToRolesByNameAsync(user, roleNames, assignedAt, cancellationToken);
        }

        public Task AcquireAccountProvisioningLockAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AcquireAdminMutationLockAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken cancellationToken)
            => Task.FromResult(Users.Count(u => u.Status == AccountStatus.Active && (!excludeUserId.HasValue || u.Id != excludeUserId.Value) && u.UserRoles.Any(ur => ur.Role.RoleName == nameof(SystemRoleName.Admin))));
        public Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken)
            => Task.FromResult(Users.Any(u => u.UserRoles.Any(ur => ur.Role.RoleName == nameof(SystemRoleName.Admin))));
        public void Add(User user) => Users.Add(user);

        // IUserQueryReader
        public Task<PagedResult<UserSummaryDto>> GetUsersAsync(GetUsersQuery query, CancellationToken cancellationToken)
        {
            var filtered = Users.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(query.Role))
            {
                filtered = filtered.Where(u => u.UserRoles.Any(ur => string.Equals(ur.Role.RoleName, query.Role, StringComparison.OrdinalIgnoreCase)));
            }

            var items = filtered.Select(u => new UserSummaryDto(
                u.Id,
                u.Email,
                u.FullName,
                u.UserCode,
                u.PhoneNumber,
                u.Status.ToString(),
                u.UserRoles.Select(ur => ur.Role.RoleName).ToList(),
                u.CreatedAt,
                u.LastLoginAt)).ToList();

            return Task.FromResult(new PagedResult<UserSummaryDto>(items, items.Count, query.PageNumber, query.PageSize));
        }

        public Task<string?> ResolveRoleNameAsync(string roleName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(roleName) || long.TryParse(roleName.Trim(), out _))
            {
                return Task.FromResult<string?>(null);
            }
            var found = Roles.FirstOrDefault(r => string.Equals(r.RoleName, roleName.Trim(), StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(found?.RoleName);
        }

        // Remaining supporting fakes
        public Task<OneTimeChallenge?> GetAsync(string selector, ChallengePurpose purpose, CancellationToken cancellationToken) => Task.FromResult<OneTimeChallenge?>(null);
        public Task<OneTimeChallenge?> GetForUpdateAsync(string selector, ChallengePurpose purpose, CancellationToken cancellationToken) => Task.FromResult<OneTimeChallenge?>(null);
        public Task InvalidateOutstandingAsync(Guid userId, ChallengePurpose purpose, DateTime now, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Add(OneTimeChallenge challenge) { }
        public void EnqueueActivationEmail(Guid userId, string to, string fullName, string rawToken, DateTime now) { }
        public void EnqueuePasswordRecoveryEmail(Guid userId, string email, string fullName, string code, DateTime createdAt) { }
        public OpaqueToken CreateToken() => new("sel.val", "sel", [1, 2]);
        public NumericCode CreateNumericCode(int digits) => new("sel", "123456", [1, 2]);
        public bool TryParse(string rawToken, out string selector, out string validator) { selector = "sel"; validator = "val"; return true; }
        public bool Verify(string suppliedSecret, byte[] expectedHash) => true;
        public PasswordPolicyResult Validate(string password) => new(password.Length >= 12, []);
        public string Hash(User user, string password) => "hash:" + password;
        public bool Verify(User user, string passwordHash, string providedPassword) => passwordHash == "hash:" + providedPassword;
        public ImportFileReadResult Read(Stream stream) => new([], []);
        public byte[] Generate(SystemRoleName role) => [];
    }

    #endregion
}
