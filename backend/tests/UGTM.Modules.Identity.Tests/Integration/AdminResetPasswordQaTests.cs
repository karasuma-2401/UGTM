using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Infrastructure;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.Identity.Infrastructure.Endpoints;
using UGTM.Modules.Identity.Infrastructure.Notifications;
using UGTM.Modules.Identity.Infrastructure.Persistence;

namespace UGTM.Modules.Identity.Tests.Integration;

[Collection(PostgreSqlCollection.Name)]
public sealed class AdminResetPasswordQaTests(PostgreSqlFixture fixture)
{
    private const string AdminPassword = "QaAdmin!Password123";
    private const string UserPassword = "QaUser!Password123";
    private const string StrongCustomPassword = "QaCustom!Strong456";

    [Fact]
    public async Task Reauth_tracks_wrong_and_whitespace_passwords_skips_empty_string_and_locks_out_before_write_transaction()
    {
        await using var host = await QaHost.CreateAsync(fixture.ConnectionString);
        var (adminId, targetId) = await ResetSchemaAndSeedAsync(host.App.Services, static async (db, passwords, now) =>
        {
            var admin = CreateUser("admin.reauth@ugtm.edu.vn", "Admin Reauth", "ADQA01", AccountStatus.Active, now, passwords, AdminPassword);
            var target = CreateUser("target.reauth@ugtm.edu.vn", "Target Reauth", "SVQA01", AccountStatus.Active, now, passwords, UserPassword);
            await AddUsersWithRolesAsync(db, now, (admin, [SystemRoleName.Admin]), (target, [SystemRoleName.Student]));
            return (admin.Id, target.Id);
        });
        await host.App.StartAsync();

        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        using var client = host.App.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");
        var adminToken = await LoginAndGetTokenAsync(client, cookies, "admin.reauth@ugtm.edu.vn", AdminPassword);

        var originalTargetHash = await ReadUserFieldAsync(host.App.Services, targetId, u => u.PasswordHash);
        var originalStamp = await ReadUserFieldAsync(host.App.Services, targetId, u => u.SecurityStamp);

        // Empty adminPassword short-circuits before ReauthenticationVerifier (0 failed attempts recorded)
        using var emptyReauthRes = await SendAsync(
            client, cookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
            new { mode = "Generated", adminPassword = string.Empty }, bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.BadRequest, emptyReauthRes.StatusCode);
        Assert.Equal("Identity.ReauthenticationFailed", ReadErrorCode(await emptyReauthRes.Content.ReadAsStringAsync()));
        Assert.Equal(0, await ReadUserFieldAsync(host.App.Services, adminId, u => u.AccessFailedCount));

        // Whitespace adminPassword reaches ReauthenticationVerifier -> increments AccessFailedCount to 1
        using var whitespaceReauthRes = await SendAsync(
            client, cookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
            new { mode = "Generated", adminPassword = "   " }, bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.BadRequest, whitespaceReauthRes.StatusCode);
        Assert.Equal("Identity.ReauthenticationFailed", ReadErrorCode(await whitespaceReauthRes.Content.ReadAsStringAsync()));
        Assert.Equal(1, await ReadUserFieldAsync(host.App.Services, adminId, u => u.AccessFailedCount));

        // Next 3 wrong passwords -> counts 2, 3, 4 (still 400 ReauthenticationFailed)
        for (var expectedCount = 2; expectedCount <= 4; expectedCount++)
        {
            using var badRes = await SendAsync(
                client, cookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
                new { mode = "Generated", adminPassword = $"WrongPass!{expectedCount}" }, bearerToken: adminToken);
            Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);
            Assert.Equal("Identity.ReauthenticationFailed", ReadErrorCode(await badRes.Content.ReadAsStringAsync()));
            Assert.Equal(expectedCount, await ReadUserFieldAsync(host.App.Services, adminId, u => u.AccessFailedCount));
            Assert.Null(await ReadUserFieldAsync(host.App.Services, adminId, u => u.LockoutEndAt));
        }

        // 5th failed attempt locks the admin account -> 429 Identity.AccountLocked
        using var fifthFailRes = await SendAsync(
            client, cookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
            new { mode = "Generated", adminPassword = "WrongPass!5" }, bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, fifthFailRes.StatusCode);
        Assert.Equal("Identity.AccountLocked", ReadErrorCode(await fifthFailRes.Content.ReadAsStringAsync()));
        Assert.NotNull(await ReadUserFieldAsync(host.App.Services, adminId, u => u.LockoutEndAt));

        // Even the correct adminPassword is rejected with 429 while locked out
        using var lockedValidRes = await SendAsync(
            client, cookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
            new { mode = "Generated", adminPassword = AdminPassword }, bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, lockedValidRes.StatusCode);
        Assert.Equal("Identity.AccountLocked", ReadErrorCode(await lockedValidRes.Content.ReadAsStringAsync()));

        // Target account remained untouched across all failed/locked calls
        Assert.Equal(originalTargetHash, await ReadUserFieldAsync(host.App.Services, targetId, u => u.PasswordHash));
        Assert.Equal(originalStamp, await ReadUserFieldAsync(host.App.Services, targetId, u => u.SecurityStamp));
        Assert.False(await ReadUserFieldAsync(host.App.Services, targetId, u => u.MustChangePassword));
    }

    [Fact]
    public async Task Rbac_non_admin_manager_can_reset_student_but_is_blocked_from_multi_role_admin_and_audited_as_denied()
    {
        await using var host = await QaHost.CreateAsync(fixture.ConnectionString);
        var (adminId, managerId, studentId, multiRoleAdminId) = await ResetSchemaAndSeedAsync(host.App.Services, static async (db, passwords, now) =>
        {
            var admin = CreateUser("admin.rbac@ugtm.edu.vn", "Admin RBAC", "ADQA02", AccountStatus.Active, now, passwords, AdminPassword);
            var manager = CreateUser("manager.rbac@ugtm.edu.vn", "Academic Manager", "MGQA02", AccountStatus.Active, now, passwords, UserPassword);
            var student = CreateUser("student.rbac@ugtm.edu.vn", "Student RBAC", "SVQA02", AccountStatus.Active, now, passwords, UserPassword);
            var multiAdmin = CreateUser("multi.admin@ugtm.edu.vn", "Multi Role Admin", "ADQA03", AccountStatus.Active, now, passwords, UserPassword);

            await AddUsersWithRolesAsync(
                db,
                now,
                (admin, [SystemRoleName.Admin]),
                (manager, [SystemRoleName.AcademicAffairs]),
                (student, [SystemRoleName.Student]),
                (multiAdmin, [SystemRoleName.Lecturer, SystemRoleName.Admin]));

            var managePerm = await db.Permissions.FirstOrDefaultAsync(p => p.PermissionCode == PermissionCodes.IdentityUsersManage);
            if (managePerm is null)
            {
                managePerm = new Permission
                {
                    PermissionCode = PermissionCodes.IdentityUsersManage,
                    PermissionName = "Quản lý người dùng",
                    Module = "Identity"
                };
                db.Permissions.Add(managePerm);
                await db.SaveChangesAsync();
            }

            var academicRoleId = await db.Roles.Where(r => r.RoleName == nameof(SystemRoleName.AcademicAffairs)).Select(r => r.Id).SingleAsync();
            db.RolePermissions.Add(new RolePermission { RoleId = academicRoleId, PermissionId = managePerm.Id, AssignedAt = now });
            await db.SaveChangesAsync();
            return (admin.Id, manager.Id, student.Id, multiAdmin.Id);
        });
        await host.App.StartAsync();

        var mgrCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        var peerCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        var admCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        using var client = host.App.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");

        var mgrToken = await LoginAndGetTokenAsync(client, mgrCookies, "manager.rbac@ugtm.edu.vn", UserPassword);
        var multiAdminOldToken = await LoginAndGetTokenAsync(client, peerCookies, "multi.admin@ugtm.edu.vn", UserPassword);
        var adminToken = await LoginAndGetTokenAsync(client, admCookies, "admin.rbac@ugtm.edu.vn", AdminPassword);
        var multiAdminOriginalHash = await ReadUserFieldAsync(host.App.Services, multiRoleAdminId, u => u.PasswordHash);

        // Manager WITH identity.users.manage resets Student -> 200 OK
        using var resetStudentRes = await SendAsync(
            client, mgrCookies, HttpMethod.Post, $"/api/users/{studentId}/reset-password",
            new { mode = "Custom", newPassword = StrongCustomPassword, requirePasswordChange = true, adminPassword = UserPassword },
            bearerToken: mgrToken);
        Assert.Equal(HttpStatusCode.OK, resetStudentRes.StatusCode);

        // Non-Admin manager CANNOT reset multi-role [Lecturer, Admin] -> 403 Identity.CannotManageAdminPassword
        using var escalateRes = await SendAsync(
            client, mgrCookies, HttpMethod.Post, $"/api/users/{multiRoleAdminId}/reset-password",
            new { mode = "Generated", adminPassword = UserPassword },
            bearerToken: mgrToken);
        Assert.Equal(HttpStatusCode.Forbidden, escalateRes.StatusCode);
        Assert.Equal("Identity.CannotManageAdminPassword", ReadErrorCode(await escalateRes.Content.ReadAsStringAsync()));

        // Multi-role Admin's password and active session were NOT touched
        Assert.Equal(multiAdminOriginalHash, await ReadUserFieldAsync(host.App.Services, multiRoleAdminId, u => u.PasswordHash));
        using var stillActiveMe = await SendAsync(client, peerCookies, HttpMethod.Get, "/api/identity/auth/me", bearerToken: multiAdminOldToken);
        Assert.Equal(HttpStatusCode.OK, stillActiveMe.StatusCode);

        // Denied audit log recorded for escalation attempt
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var deniedAudit = await db.AuditLogs.AsNoTracking()
                .SingleAsync(a => a.ActorUserId == managerId && a.Action == AuditActions.UserPasswordResetByAdmin && a.Outcome == AuditOutcomes.Denied);
            Assert.Equal("Identity.CannotManageAdminPassword", deniedAudit.ReasonCode);
            Assert.Equal(multiRoleAdminId.ToString("D"), deniedAudit.TargetId);
        }

        // Peer Admin CAN reset multi-role [Lecturer, Admin] -> 200 OK and revokes session
        using var peerResetRes = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{multiRoleAdminId}/reset-password",
            new { mode = "Generated", adminPassword = AdminPassword },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, peerResetRes.StatusCode);

        using var revokedMe = await SendAsync(client, peerCookies, HttpMethod.Get, "/api/identity/auth/me", bearerToken: multiAdminOldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedMe.StatusCode);
        _ = adminId;
    }

    [Fact]
    public async Task Target_status_preserved_for_suspended_and_disabled_while_locked_active_target_lockout_is_cleared()
    {
        await using var host = await QaHost.CreateAsync(fixture.ConnectionString);
        var (suspendedId, disabledId, lockedId) = await ResetSchemaAndSeedAsync(host.App.Services, static async (db, passwords, now) =>
        {
            var admin = CreateUser("admin.status@ugtm.edu.vn", "Admin Status", "ADQA04", AccountStatus.Active, now, passwords, AdminPassword);
            var suspended = CreateUser("suspended.qa@ugtm.edu.vn", "Suspended Target", "SVQA03", AccountStatus.Active, now, passwords, UserPassword);
            suspended.Suspend(now.AddMinutes(-5));
            var disabled = CreateUser("disabled.qa@ugtm.edu.vn", "Disabled Target", "SVQA04", AccountStatus.Active, now, passwords, UserPassword);
            disabled.Disable(now.AddMinutes(-5));
            var locked = CreateUser("locked.qa@ugtm.edu.vn", "Locked Target", "SVQA05", AccountStatus.Active, now, passwords, UserPassword);
            for (var i = 0; i < 5; i++)
            {
                locked.RecordFailedLogin(now, 5, TimeSpan.FromMinutes(15));
            }

            await AddUsersWithRolesAsync(
                db,
                now,
                (admin, [SystemRoleName.Admin]),
                (suspended, [SystemRoleName.Student]),
                (disabled, [SystemRoleName.Student]),
                (locked, [SystemRoleName.Student]));
            return (suspended.Id, disabled.Id, locked.Id);
        });
        await host.App.StartAsync();

        var admCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        using var client = host.App.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");
        var adminToken = await LoginAndGetTokenAsync(client, admCookies, "admin.status@ugtm.edu.vn", AdminPassword);

        // Suspended user reset -> 200 OK, status remains Suspended, login returns anti-enumeration 401 while auditing AccountSuspended
        using var susReset = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{suspendedId}/reset-password",
            new { mode = "Custom", newPassword = StrongCustomPassword, requirePasswordChange = true, adminPassword = AdminPassword },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, susReset.StatusCode);
        Assert.Equal(AccountStatus.Suspended, await ReadUserFieldAsync(host.App.Services, suspendedId, u => u.Status));
        Assert.True(await ReadUserFieldAsync(host.App.Services, suspendedId, u => u.MustChangePassword));

        var susCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        var susCsrf = await GetCsrfAsync(client, susCookies);
        using var susLogin = await SendAsync(client, susCookies, HttpMethod.Post, "/api/identity/auth/login", new { identifier = "suspended.qa@ugtm.edu.vn", password = StrongCustomPassword }, susCsrf);
        Assert.Equal(HttpStatusCode.Unauthorized, susLogin.StatusCode);
        Assert.Equal("Identity.InvalidCredentials", ReadErrorCode(await susLogin.Content.ReadAsStringAsync()));

        // Disabled user reset -> 200 OK, status remains Disabled, login returns anti-enumeration 401 while auditing AccountDisabled
        using var disReset = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{disabledId}/reset-password",
            new { mode = "Generated", requirePasswordChange = false, adminPassword = AdminPassword },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, disReset.StatusCode);
        Assert.Equal(AccountStatus.Disabled, await ReadUserFieldAsync(host.App.Services, disabledId, u => u.Status));
        Assert.False(await ReadUserFieldAsync(host.App.Services, disabledId, u => u.MustChangePassword));
        var disTempPass = ReadString(await disReset.Content.ReadAsStringAsync(), "data", "temporaryPassword");

        var disCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        var disCsrf = await GetCsrfAsync(client, disCookies);
        using var disLogin = await SendAsync(client, disCookies, HttpMethod.Post, "/api/identity/auth/login", new { identifier = "disabled.qa@ugtm.edu.vn", password = disTempPass }, disCsrf);
        Assert.Equal(HttpStatusCode.Unauthorized, disLogin.StatusCode);
        Assert.Equal("Identity.InvalidCredentials", ReadErrorCode(await disLogin.Content.ReadAsStringAsync()));

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var susAudit = await db.AuditLogs.AsNoTracking()
                .SingleAsync(a => a.Action == AuditActions.Login && a.Outcome == AuditOutcomes.Denied && a.TargetId == suspendedId.ToString("D"));
            Assert.Equal("Identity.AccountSuspended", susAudit.ReasonCode);

            var disAudit = await db.AuditLogs.AsNoTracking()
                .SingleAsync(a => a.Action == AuditActions.Login && a.Outcome == AuditOutcomes.Denied && a.TargetId == disabledId.ToString("D"));
            Assert.Equal("Identity.AccountDisabled", disAudit.ReasonCode);
        }

        // Locked Active user reset -> 200 OK, LockoutEndAt=null & AccessFailedCount=0, immediately logs in
        Assert.NotNull(await ReadUserFieldAsync(host.App.Services, lockedId, u => u.LockoutEndAt));
        using var lockedReset = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{lockedId}/reset-password",
            new { mode = "Custom", newPassword = StrongCustomPassword, requirePasswordChange = false, adminPassword = AdminPassword },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, lockedReset.StatusCode);
        Assert.Equal(0, await ReadUserFieldAsync(host.App.Services, lockedId, u => u.AccessFailedCount));
        Assert.Null(await ReadUserFieldAsync(host.App.Services, lockedId, u => u.LockoutEndAt));

        var lockedCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        var lockedToken = await LoginAndGetTokenAsync(client, lockedCookies, "locked.qa@ugtm.edu.vn", StrongCustomPassword);
        Assert.False(string.IsNullOrWhiteSpace(lockedToken));
    }

    [Fact]
    public async Task Input_boundaries_whitespace_reasons_long_passwords_and_missing_target_behave_as_implemented()
    {
        await using var host = await QaHost.CreateAsync(fixture.ConnectionString);
        var targetId = await ResetSchemaAndSeedAsync(host.App.Services, static async (db, passwords, now) =>
        {
            var admin = CreateUser("admin.bounds@ugtm.edu.vn", "Admin Bounds", "ADQA05", AccountStatus.Active, now, passwords, AdminPassword);
            var target = CreateUser("target.bounds@ugtm.edu.vn", "Target Bounds", "SVQA06", AccountStatus.Active, now, passwords, UserPassword);
            await AddUsersWithRolesAsync(db, now, (admin, [SystemRoleName.Admin]), (target, [SystemRoleName.Student]));
            return target.Id;
        });
        await host.App.StartAsync();

        var admCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        using var client = host.App.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");
        var adminToken = await LoginAndGetTokenAsync(client, admCookies, "admin.bounds@ugtm.edu.vn", AdminPassword);

        // Mode with surrounding whitespace & mixed case + empty string newPassword -> accepted (string.IsNullOrEmpty("") == true)
        using var trimmedGenRes = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
            new { mode = "  gEnErAtEd  ", newPassword = "", requirePasswordChange = true, adminPassword = AdminPassword, reason = "   " },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, trimmedGenRes.StatusCode);
        var genJson = await trimmedGenRes.Content.ReadAsStringAsync();
        Assert.Equal("Generated", ReadString(genJson, "data", "mode"));

        // Mode Generated with whitespace-only newPassword -> 400 InvalidInput (!string.IsNullOrEmpty("   "))
        using var wsPassRes = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
            new { mode = "Generated", newPassword = "   ", adminPassword = AdminPassword },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.BadRequest, wsPassRes.StatusCode);
        Assert.Equal("Identity.InvalidInput", ReadErrorCode(await wsPassRes.Content.ReadAsStringAsync()));

        // Exactly 500-char reason after trimming + special characters / XSS / SQL payload
        var exact500 = new string('A', 500);
        using var trimmedReasonRes = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
            new { mode = "  cUsToM  ", newPassword = StrongCustomPassword, requirePasswordChange = true, adminPassword = AdminPassword, reason = $"  {exact500}  " },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, trimmedReasonRes.StatusCode);
        var custJson = await trimmedReasonRes.Content.ReadAsStringAsync();
        Assert.Equal("Custom", ReadString(custJson, "data", "mode"));
        Assert.Equal(JsonValueKind.Null, ReadElement(custJson, "data", "temporaryPassword").ValueKind);

        var xssSqlReason = "<script>alert(1)</script>'; DROP TABLE identity.\"user\"; -- Tiếng Việt";
        using var xssReasonRes = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
            new { mode = "Generated", requirePasswordChange = false, adminPassword = AdminPassword, reason = xssSqlReason },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, xssReasonRes.StatusCode);

        // PasswordPolicy hardening: space-only or tab-only special char and >128 chars rejected (400 WeakPassword)
        foreach (var rejectedPassword in new[]
                 {
                     "Abcdefghijk1 ",
                     "Abcdefghijk1\t",
                     "Aa1!" + new string('x', 125),
                     "Aa1!" + new string('x', 4996)
                 })
        {
            using var rejectedRes = await SendAsync(
                client, admCookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
                new { mode = "Custom", newPassword = rejectedPassword, requirePasswordChange = false, adminPassword = AdminPassword },
                bearerToken: adminToken);
            Assert.Equal(HttpStatusCode.BadRequest, rejectedRes.StatusCode);
            Assert.Equal("Identity.WeakPassword", ReadErrorCode(await rejectedRes.Content.ReadAsStringAsync()));
        }

        // Passphrase with spaces AND visible special char succeeds; exact 128-char boundary succeeds
        using var passphraseRes = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
            new { mode = "Custom", newPassword = "My Strong Passphrase 2026!", requirePasswordChange = false, adminPassword = AdminPassword },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, passphraseRes.StatusCode);

        var exact128Password = "Aa1!" + new string('x', 124);
        using var exact128Res = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{targetId}/reset-password",
            new { mode = "Custom", newPassword = exact128Password, requirePasswordChange = false, adminPassword = AdminPassword },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, exact128Res.StatusCode);

        // Verify login works with the exact 128-char boundary password
        var tgtCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        var tgtToken = await LoginAndGetTokenAsync(client, tgtCookies, "target.bounds@ugtm.edu.vn", exact128Password);
        Assert.False(string.IsNullOrWhiteSpace(tgtToken));

        // Missing target -> 404 UserNotFound with 0 audit/outbox entries for missing GUID
        var missingId = Guid.NewGuid();
        using var missingRes = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{missingId}/reset-password",
            new { mode = "Generated", adminPassword = AdminPassword },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.NotFound, missingRes.StatusCode);
        Assert.Equal("Identity.UserNotFound", ReadErrorCode(await missingRes.Content.ReadAsStringAsync()));

        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var audits = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == AuditActions.UserPasswordResetByAdmin && a.TargetId == targetId.ToString("D"))
            .OrderBy(a => a.Id)
            .ToListAsync();
        Assert.Equal(5, audits.Count);
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(audits[0].MetadataJson!).RootElement.GetProperty("Reason").ValueKind);
        Assert.Equal(exact500, JsonDocument.Parse(audits[1].MetadataJson!).RootElement.GetProperty("Reason").GetString());
        Assert.Equal(xssSqlReason, JsonDocument.Parse(audits[2].MetadataJson!).RootElement.GetProperty("Reason").GetString());

        Assert.Equal(0, await db.AuditLogs.AsNoTracking().CountAsync(a => a.TargetId == missingId.ToString("D")));
    }

    [Fact]
    public async Task Real_jwt_enforces_hard_gate_on_admin_allows_no_change_flag_and_supports_logout_and_refresh()
    {
        await using var host = await QaHost.CreateAsync(fixture.ConnectionString, useRealJwt: true);
        var (peerAdminId, studentId) = await ResetSchemaAndSeedAsync(host.App.Services, static async (db, passwords, now) =>
        {
            var rootAdmin = CreateUser("root.jwt@ugtm.edu.vn", "Root Admin", "ADQA06", AccountStatus.Active, now, passwords, AdminPassword);
            var peerAdmin = CreateUser("peer.jwt@ugtm.edu.vn", "Peer Admin", "ADQA07", AccountStatus.Active, now, passwords, UserPassword);
            var student = CreateUser("student.jwt@ugtm.edu.vn", "Student JWT", "SVQA07", AccountStatus.Active, now, passwords, UserPassword);
            await AddUsersWithRolesAsync(
                db,
                now,
                (rootAdmin, [SystemRoleName.Admin]),
                (peerAdmin, [SystemRoleName.Admin]),
                (student, [SystemRoleName.Student]));
            return (peerAdmin.Id, student.Id);
        });
        await host.App.StartAsync();

        var rootCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        using var client = host.App.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");
        var rootToken = await LoginAndGetTokenAsync(client, rootCookies, "root.jwt@ugtm.edu.vn", AdminPassword);

        // A. requirePasswordChange = false -> real JWT has NO pwd_change_required claim & /sessions returns 200 OK
        using var resetNoChange = await SendAsync(
            client, rootCookies, HttpMethod.Post, $"/api/users/{studentId}/reset-password",
            new { mode = "Custom", newPassword = StrongCustomPassword, requirePasswordChange = false, adminPassword = AdminPassword },
            bearerToken: rootToken);
        Assert.Equal(HttpStatusCode.OK, resetNoChange.StatusCode);

        var stuCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        var stuToken = await LoginAndGetTokenAsync(client, stuCookies, "student.jwt@ugtm.edu.vn", StrongCustomPassword);
        var stuJwt = new JwtSecurityTokenHandler().ReadJwtToken(stuToken);
        Assert.DoesNotContain(stuJwt.Claims, c => c.Type == IdentityClaimTypes.PasswordChangeRequired);
        using var stuSessions = await SendAsync(client, stuCookies, HttpMethod.Get, "/api/identity/auth/sessions", bearerToken: stuToken);
        Assert.Equal(HttpStatusCode.OK, stuSessions.StatusCode);

        // B. Peer Admin reset with requirePasswordChange = true -> blocked from Admin endpoints (/api/users & /reset-password)
        using var resetPeerAdmin = await SendAsync(
            client, rootCookies, HttpMethod.Post, $"/api/users/{peerAdminId}/reset-password",
            new { mode = "Generated", requirePasswordChange = true, adminPassword = AdminPassword },
            bearerToken: rootToken);
        Assert.Equal(HttpStatusCode.OK, resetPeerAdmin.StatusCode);
        var peerTempPass = ReadString(await resetPeerAdmin.Content.ReadAsStringAsync(), "data", "temporaryPassword");

        var peerCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        var peerCsrf = await GetCsrfAsync(client, peerCookies);
        using var peerLogin = await SendAsync(
            client, peerCookies, HttpMethod.Post, "/api/identity/auth/login",
            new { identifier = "peer.jwt@ugtm.edu.vn", password = peerTempPass },
            csrfHeader: peerCsrf);
        Assert.Equal(HttpStatusCode.OK, peerLogin.StatusCode);
        var peerToken = ReadString(await peerLogin.Content.ReadAsStringAsync(), "data", "accessToken");

        var peerJwt = new JwtSecurityTokenHandler().ReadJwtToken(peerToken);
        Assert.Equal("true", peerJwt.Claims.Single(c => c.Type == IdentityClaimTypes.PasswordChangeRequired).Value);

        // Hard Gate blocks GET /api/users AND POST /api/users/{id}/reset-password
        using var blockedUsers = await SendAsync(client, peerCookies, HttpMethod.Get, "/api/users", bearerToken: peerToken);
        Assert.Equal(HttpStatusCode.Forbidden, blockedUsers.StatusCode);
        Assert.Equal("Identity.PasswordChangeRequired", ReadErrorCode(await blockedUsers.Content.ReadAsStringAsync()));
        Assert.Contains("no-store", blockedUsers.Headers.CacheControl!.ToString(), StringComparison.Ordinal);

        using var blockedAdminReset = await SendAsync(
            client, peerCookies, HttpMethod.Post, $"/api/users/{studentId}/reset-password",
            new { mode = "Generated", adminPassword = peerTempPass },
            bearerToken: peerToken);
        Assert.Equal(HttpStatusCode.Forbidden, blockedAdminReset.StatusCode);
        Assert.Equal("Identity.PasswordChangeRequired", ReadErrorCode(await blockedAdminReset.Content.ReadAsStringAsync()));

        // /me allowed and Refresh preserves mustChangePassword & remains gated
        using var allowedMe = await SendAsync(client, peerCookies, HttpMethod.Get, "/api/identity/auth/me", bearerToken: peerToken);
        Assert.Equal(HttpStatusCode.OK, allowedMe.StatusCode);

        using var refreshRes = await SendAsync(client, peerCookies, HttpMethod.Post, "/api/identity/auth/refresh", csrfHeader: peerCsrf);
        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);
        var refreshedJson = await refreshRes.Content.ReadAsStringAsync();
        Assert.True(ReadBoolean(refreshedJson, "data", "user", "mustChangePassword"));
        var refreshedToken = ReadString(refreshedJson, "data", "accessToken");

        using var gatedAfterRefresh = await SendAsync(client, peerCookies, HttpMethod.Get, "/api/users", bearerToken: refreshedToken);
        Assert.Equal(HttpStatusCode.Forbidden, gatedAfterRefresh.StatusCode);
        Assert.Equal("Identity.PasswordChangeRequired", ReadErrorCode(await gatedAfterRefresh.Content.ReadAsStringAsync()));

        // Logout succeeds while gated and invalidates the refreshed token immediately via SessionValidator
        using var logoutRes = await SendAsync(client, peerCookies, HttpMethod.Post, "/api/identity/auth/logout", csrfHeader: peerCsrf);
        Assert.Equal(HttpStatusCode.OK, logoutRes.StatusCode);

        using var afterLogoutMe = await SendAsync(client, peerCookies, HttpMethod.Get, "/api/identity/auth/me", bearerToken: refreshedToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogoutMe.StatusCode);
    }

    [Fact]
    public async Task Self_recovery_clears_must_change_password_and_change_password_rejects_reusing_temp_password()
    {
        await using var host = await QaHost.CreateAsync(fixture.ConnectionString, useRealJwt: true);
        var (student1Id, student2Id) = await ResetSchemaAndSeedAsync(host.App.Services, static async (db, passwords, now) =>
        {
            var admin = CreateUser("admin.rec@ugtm.edu.vn", "Admin Recovery", "ADQA08", AccountStatus.Active, now, passwords, AdminPassword);
            var s1 = CreateUser("student1.rec@ugtm.edu.vn", "Student One", "SVQA08", AccountStatus.Active, now, passwords, UserPassword);
            var s2 = CreateUser("student2.rec@ugtm.edu.vn", "Student Two", "SVQA09", AccountStatus.Active, now, passwords, UserPassword);
            await AddUsersWithRolesAsync(
                db,
                now,
                (admin, [SystemRoleName.Admin]),
                (s1, [SystemRoleName.Student]),
                (s2, [SystemRoleName.Student]));
            return (s1.Id, s2.Id);
        });
        await host.App.StartAsync();

        var admCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        using var client = host.App.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");
        var adminToken = await LoginAndGetTokenAsync(client, admCookies, "admin.rec@ugtm.edu.vn", AdminPassword);

        // Reset both students with requirePasswordChange = true
        using var reset1 = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{student1Id}/reset-password",
            new { mode = "Generated", requirePasswordChange = true, adminPassword = AdminPassword },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, reset1.StatusCode);

        using var reset2 = await SendAsync(
            client, admCookies, HttpMethod.Post, $"/api/users/{student2Id}/reset-password",
            new { mode = "Generated", requirePasswordChange = true, adminPassword = AdminPassword },
            bearerToken: adminToken);
        Assert.Equal(HttpStatusCode.OK, reset2.StatusCode);
        var temp2 = ReadString(await reset2.Content.ReadAsStringAsync(), "data", "temporaryPassword");

        // C. Student 1 completes self-service recovery -> clears mustChangePassword = false
        var s1Cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        using var recStart = await SendAsync(client, s1Cookies, HttpMethod.Post, "/api/identity/auth/recovery/start", new { email = "student1.rec@ugtm.edu.vn" });
        Assert.Equal(HttpStatusCode.OK, recStart.StatusCode);
        var challengeId = ReadString(await recStart.Content.ReadAsStringAsync(), "data", "challengeId");

        string otpCode;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var dp = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
            var otpMsg = await db.IdentityOutboxMessages.AsNoTracking().SingleAsync(m => m.Type == "recovery-otp");
            var decrypted = dp.CreateProtector(otpMsg.ProtectionPurpose).Unprotect(otpMsg.ProtectedPayload);
            otpCode = JsonSerializer.Deserialize<IdentityEmailPayload>(decrypted)!.Secret;
        }

        using var recVerify = await SendAsync(client, s1Cookies, HttpMethod.Post, "/api/identity/auth/recovery/verify", new { challengeId, code = otpCode });
        Assert.Equal(HttpStatusCode.OK, recVerify.StatusCode);
        var resetGrant = ReadString(await recVerify.Content.ReadAsStringAsync(), "data", "resetGrant");

        using var recReset = await SendAsync(client, s1Cookies, HttpMethod.Post, "/api/identity/auth/recovery/reset", new { resetGrant, newPassword = StrongCustomPassword });
        Assert.Equal(HttpStatusCode.OK, recReset.StatusCode);
        Assert.False(await ReadUserFieldAsync(host.App.Services, student1Id, u => u.MustChangePassword));

        var s1Token = await LoginAndGetTokenAsync(client, s1Cookies, "student1.rec@ugtm.edu.vn", StrongCustomPassword);
        using var s1Sessions = await SendAsync(client, s1Cookies, HttpMethod.Get, "/api/identity/auth/sessions", bearerToken: s1Token);
        Assert.Equal(HttpStatusCode.OK, s1Sessions.StatusCode);

        // D. Student 2 cannot reuse temporary password via /change-password; only rotating to a distinct strong password clears the gate
        var s2Cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        var s2Token = await LoginAndGetTokenAsync(client, s2Cookies, "student2.rec@ugtm.edu.vn", temp2);
        using var samePassChange = await SendAsync(
            client, s2Cookies, HttpMethod.Post, "/api/identity/auth/change-password",
            new { currentPassword = temp2, newPassword = temp2 },
            bearerToken: s2Token);
        Assert.Equal(HttpStatusCode.BadRequest, samePassChange.StatusCode);
        Assert.Equal("Identity.PasswordMustDifferFromCurrent", ReadErrorCode(await samePassChange.Content.ReadAsStringAsync()));
        Assert.True(await ReadUserFieldAsync(host.App.Services, student2Id, u => u.MustChangePassword));

        using var stillGatedSessions = await SendAsync(client, s2Cookies, HttpMethod.Get, "/api/identity/auth/sessions", bearerToken: s2Token);
        Assert.Equal(HttpStatusCode.Forbidden, stillGatedSessions.StatusCode);
        Assert.Equal("Identity.PasswordChangeRequired", ReadErrorCode(await stillGatedSessions.Content.ReadAsStringAsync()));

        using var stillAllowedMe = await SendAsync(client, s2Cookies, HttpMethod.Get, "/api/identity/auth/me", bearerToken: s2Token);
        Assert.Equal(HttpStatusCode.OK, stillAllowedMe.StatusCode);

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var deniedChange = await db.AuditLogs.AsNoTracking()
                .SingleAsync(a => a.Action == AuditActions.PasswordChanged && a.Outcome == AuditOutcomes.Denied && a.TargetId == student2Id.ToString("D"));
            Assert.Equal("Identity.PasswordMustDifferFromCurrent", deniedChange.ReasonCode);
        }

        const string rotatedPassword = "StudentRotated!789";
        using var validChange = await SendAsync(
            client, s2Cookies, HttpMethod.Post, "/api/identity/auth/change-password",
            new { currentPassword = temp2, newPassword = rotatedPassword },
            bearerToken: s2Token);
        Assert.Equal(HttpStatusCode.OK, validChange.StatusCode);
        Assert.False(await ReadUserFieldAsync(host.App.Services, student2Id, u => u.MustChangePassword));

        using var revokedOldMe = await SendAsync(client, s2Cookies, HttpMethod.Get, "/api/identity/auth/me", bearerToken: s2Token);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedOldMe.StatusCode);

        var s2FinalToken = await LoginAndGetTokenAsync(client, s2Cookies, "student2.rec@ugtm.edu.vn", rotatedPassword);
        using var s2RestoredSessions = await SendAsync(client, s2Cookies, HttpMethod.Get, "/api/identity/auth/sessions", bearerToken: s2FinalToken);
        Assert.Equal(HttpStatusCode.OK, s2RestoredSessions.StatusCode);
    }

    private sealed class RealUtcClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    private sealed class QaHost(WebApplication app, string? tempPemPath) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;

        public static async Task<QaHost> CreateAsync(string connectionString, bool useRealJwt = true)
        {
            string? pemPath = null;
            if (useRealJwt)
            {
                using var rsa = RSA.Create(2048);
                pemPath = Path.Combine(Path.GetTempPath(), $"ugtm-qa-jwt-{Guid.NewGuid():N}.pem");
                await File.WriteAllTextAsync(pemPath, rsa.ExportPkcs8PrivateKeyPem());
            }

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Identity:Jwt:Issuer"] = "UGTM.QA",
                ["Identity:Jwt:Audience"] = "UGTM.QA",
                ["Identity:Jwt:PrivateKeyPath"] = pemPath ?? "unused.pem",
                ["Identity:Jwt:KeyId"] = "qa-key-1",
                ["Identity:Jwt:AccessTokenMinutes"] = "10",
                ["Identity:Email:PublicWebBaseUrl"] = "https://ugtm.example",
                ["Identity:Email:OutboxPartition"] = "qa-tests",
                ["Identity:ClientMetadata:IpHashKey"] = "qa-tests-ip-hash-key-at-least-32-chars",
                ["Identity:ClientMetadata:UserAgentMaxLength"] = "300"
            });

            var identity = new IdentityModule();
            identity.RegisterModule(builder.Services, builder.Configuration);
            builder.Services.RemoveAll<IHostedService>();
            builder.Services.RemoveAll<IClock>();
            builder.Services.AddSingleton<IClock>(new RealUtcClock(DateTime.UtcNow));

            var app = builder.Build();
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseIdentityPasswordChangeEnforcement();
            app.UseAuthorization();
            app.MapIdentityAuthEndpoints();
            app.MapIdentityUserEndpoints();

            return new QaHost(app, pemPath);
        }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            if (tempPemPath is not null && File.Exists(tempPemPath))
            {
                File.Delete(tempPemPath);
            }
        }
    }

    private static async Task<TResult> ResetSchemaAndSeedAsync<TResult>(
        IServiceProvider services,
        Func<IdentityDbContext, IPasswordService, DateTime, Task<TResult>> seed)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;

        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS identity CASCADE;");
        await db.Database.EnsureCreatedAsync();

        return await seed(db, passwords, now);
    }

    private static User CreateUser(
        string email,
        string fullName,
        string userCode,
        AccountStatus status,
        DateTime now,
        IPasswordService passwords,
        string plainPassword)
    {
        var user = User.Create(
            Guid.NewGuid(),
            email,
            email.ToUpperInvariant(),
            fullName,
            userCode,
            null,
            null,
            AccountStatus.PendingActivation,
            now.AddDays(-1));
        user.Activate(passwords.Hash(user, plainPassword), now.AddDays(-1));
        if (status == AccountStatus.Suspended) user.Suspend(now.AddHours(-1));
        if (status == AccountStatus.Disabled) user.Disable(now.AddHours(-1));
        return user;
    }

    private static async Task AddUsersWithRolesAsync(
        IdentityDbContext db,
        DateTime now,
        params (User User, SystemRoleName[] Roles)[] entries)
    {
        var rolesByName = await db.Roles.ToDictionaryAsync(r => r.RoleName, r => r.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var (user, roles) in entries)
        {
            db.Users.Add(user);
            foreach (var role in roles)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = rolesByName[role.ToString()],
                    AssignedAt = now
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private static async Task<TValue> ReadUserFieldAsync<TValue>(
        IServiceProvider services,
        Guid userId,
        Func<User, TValue> selector)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        return selector(user);
    }

    private static async Task<string> GetCsrfAsync(HttpClient client, IDictionary<string, string> cookies)
    {
        using var res = await SendAsync(client, cookies, HttpMethod.Get, "/api/identity/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return ReadString(await res.Content.ReadAsStringAsync(), "data", "token");
    }

    private static async Task<string> LoginAndGetTokenAsync(
        HttpClient client,
        IDictionary<string, string> cookies,
        string identifier,
        string password)
    {
        var csrf = await GetCsrfAsync(client, cookies);
        using var res = await SendAsync(
            client, cookies, HttpMethod.Post, "/api/identity/auth/login",
            new { identifier, password }, csrfHeader: csrf);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return ReadString(await res.Content.ReadAsStringAsync(), "data", "accessToken");
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
                string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}")));
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
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            foreach (var value in values)
            {
                var pair = value.Split(';', 2)[0];
                var sep = pair.IndexOf('=');
                if (sep <= 0) continue;
                var key = pair[..sep];
                var val = pair[(sep + 1)..];
                if (val.Length == 0) cookies.Remove(key);
                else cookies[key] = val;
            }
        }

        return response;
    }

    private static JsonElement ReadElement(string json, params string[] path)
    {
        using var doc = JsonDocument.Parse(json);
        var current = doc.RootElement;
        foreach (var segment in path)
        {
            current = current.GetProperty(segment);
        }

        return current.Clone();
    }

    private static string ReadString(string json, params string[] path)
        => ReadElement(json, path).GetString()!;

    private static bool ReadBoolean(string json, params string[] path)
        => ReadElement(json, path).GetBoolean();

    private static string? ReadErrorCode(string json)
        => JsonDocument.Parse(json).RootElement.GetProperty("error").GetProperty("code").GetString();
}
