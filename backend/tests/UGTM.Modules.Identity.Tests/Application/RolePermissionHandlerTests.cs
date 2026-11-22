using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Permissions.GetPermissions;
using UGTM.Modules.Identity.Application.Roles.CreateRole;
using UGTM.Modules.Identity.Application.Roles.GetRolePermissions;
using UGTM.Modules.Identity.Application.Roles.GetRoles;
using UGTM.Modules.Identity.Application.Roles.UpdateRolePermissions;
using UGTM.Modules.Identity.Application.Roles.UpdateRoleVisibility;
using UGTM.Modules.Identity.Application.Roles.UpdateRoleDescription;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Tests.Application;

public sealed class RolePermissionHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPermissionsHandler_returns_grouped_modules_and_permissions()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Permissions.AddRange([
            new Permission { Id = 1, PermissionCode = "identity.users.read", PermissionName = "Xem User", Module = "Identity" },
            new Permission { Id = 2, PermissionCode = "identity.users.manage", PermissionName = "Sửa User", Module = "Identity" },
            new Permission { Id = 3, PermissionCode = "thesis.topics.read", PermissionName = "Xem Đề tài", Module = "ThesisManagement" }
        ]);

        var handler = new GetPermissionsHandler(repo);
        var result = await handler.HandleAsync(new GetPermissionsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("Identity", result.Value[0].Module);
        Assert.Equal(2, result.Value[0].Permissions.Count);
        Assert.Equal("ThesisManagement", result.Value[1].Module);
        Assert.Single(result.Value[1].Permissions);
    }

    [Fact]
    public async Task GetRolePermissionsHandler_returns_role_and_its_permissions()
    {
        var repo = new FakeRolePermissionRepository();
        var perm1 = new Permission { Id = 1, PermissionCode = "identity.users.read", PermissionName = "Xem User", Module = "Identity" };
        var perm2 = new Permission { Id = 2, PermissionCode = "identity.users.manage", PermissionName = "Sửa User", Module = "Identity" };
        repo.Permissions.AddRange([perm1, perm2]);

        var lecturerRole = new Role { Id = 4, RoleName = nameof(SystemRoleName.Lecturer), Description = "Giảng viên" };
        repo.Roles.Add(lecturerRole);
        repo.RolePermissions.Add(new RolePermission { RoleId = 4, PermissionId = 1, Permission = perm1 });

        var handler = new GetRolePermissionsHandler(repo);
        var result = await handler.HandleAsync(new GetRolePermissionsQuery(4), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.RoleId);
        Assert.Equal("Lecturer", result.Value.RoleName);
        Assert.Single(result.Value.Permissions);
        Assert.Equal("identity.users.read", result.Value.Permissions[0].PermissionCode);
    }

    [Fact]
    public async Task GetRolePermissionsHandler_fails_when_role_not_found()
    {
        var repo = new FakeRolePermissionRepository();
        var handler = new GetRolePermissionsHandler(repo);

        var result = await handler.HandleAsync(new GetRolePermissionsQuery(999), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.RoleNotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateRolePermissionsHandler_updates_permissions_and_rotates_security_stamp()
    {
        var repo = new FakeRolePermissionRepository();
        var perm1 = new Permission { Id = 1, PermissionCode = "identity.users.read", PermissionName = "Xem User", Module = "Identity" };
        var perm2 = new Permission { Id = 2, PermissionCode = "identity.users.manage", PermissionName = "Sửa User", Module = "Identity" };
        repo.Permissions.AddRange([perm1, perm2]);

        var lecturerRole = new Role { Id = 4, RoleName = nameof(SystemRoleName.Lecturer), Description = "Giảng viên" };
        repo.Roles.Add(lecturerRole);

        var user = User.Create(Guid.NewGuid(), "gv@ugtm.edu.vn", "GV@UGTM.EDU.VN", "GV A", "GV01", null, "hash", AccountStatus.Active, Now);
        var oldStamp = user.SecurityStamp;
        repo.UsersInRoles.Add(user);

        var auditLogs = new FakeAuditLogWriter();
        var handler = new UpdateRolePermissionsHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), auditLogs);
        var result = await handler.HandleAsync(new UpdateRolePermissionsCommand(4, [1, 2]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Permissions.Count);
        Assert.True(repo.SecurityStampRotated);
        Assert.NotEqual(oldStamp, user.SecurityStamp);
        var auditEntry = Assert.Single(auditLogs.Entries);
        Assert.Equal(AuditActions.RolePermissionsChanged, auditEntry.Action);
        Assert.Equal("4", auditEntry.TargetId);
    }

    [Fact]
    public async Task UpdateRolePermissionsHandler_fails_when_role_not_found()
    {
        var repo = new FakeRolePermissionRepository();
        var handler = new UpdateRolePermissionsHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

        var result = await handler.HandleAsync(new UpdateRolePermissionsCommand(999, [1]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.RoleNotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateRolePermissionsHandler_fails_when_permission_id_invalid()
    {
        var repo = new FakeRolePermissionRepository();
        var lecturerRole = new Role { Id = 4, RoleName = nameof(SystemRoleName.Lecturer), Description = "Giảng viên" };
        repo.Roles.Add(lecturerRole);

        var handler = new UpdateRolePermissionsHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());
        var result = await handler.HandleAsync(new UpdateRolePermissionsCommand(4, [9999]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidPermission", result.Error.Code);
    }

    [Fact]
    public async Task UpdateRolePermissionsHandler_protects_admin_role_from_clearing_all_permissions()
    {
        var repo = new FakeRolePermissionRepository();
        var adminRole = new Role { Id = 1, RoleName = nameof(SystemRoleName.Admin), Description = "Quản trị viên" };
        repo.Roles.Add(adminRole);

        var handler = new UpdateRolePermissionsHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());
        var result = await handler.HandleAsync(new UpdateRolePermissionsCommand(1, []), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.CannotRevokeAllAdminPermissions", result.Error.Code);
    }

    [Fact]
    public async Task UpdateRolePermissionsHandler_handles_duplicate_permission_ids()
    {
        var repo = new FakeRolePermissionRepository();
        var perm1 = new Permission { Id = 1, PermissionCode = "identity.users.read", PermissionName = "Xem User", Module = "Identity" };
        repo.Permissions.Add(perm1);

        var lecturerRole = new Role { Id = 4, RoleName = nameof(SystemRoleName.Lecturer), Description = "Giảng viên" };
        repo.Roles.Add(lecturerRole);

        var handler = new UpdateRolePermissionsHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());
        var result = await handler.HandleAsync(new UpdateRolePermissionsCommand(4, [1, 1, 1]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Permissions);
    }

    [Fact]
    public async Task UpdateRolePermissionsHandler_allows_clearing_permissions_for_non_admin_role()
    {
        var repo = new FakeRolePermissionRepository();
        var perm1 = new Permission { Id = 1, PermissionCode = "identity.users.read", PermissionName = "Xem User", Module = "Identity" };
        repo.Permissions.Add(perm1);

        var lecturerRole = new Role { Id = 4, RoleName = nameof(SystemRoleName.Lecturer), Description = "Giảng viên" };
        repo.Roles.Add(lecturerRole);

        var handler = new UpdateRolePermissionsHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());
        var result = await handler.HandleAsync(new UpdateRolePermissionsCommand(4, []), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Permissions);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task UpdateRolePermissionsHandler_fails_when_permission_ids_contain_negative_or_zero(int invalidId)
    {
        var repo = new FakeRolePermissionRepository();
        var lecturerRole = new Role { Id = 4, RoleName = nameof(SystemRoleName.Lecturer), Description = "Giảng viên" };
        repo.Roles.Add(lecturerRole);

        var handler = new UpdateRolePermissionsHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());
        var result = await handler.HandleAsync(new UpdateRolePermissionsCommand(4, [invalidId]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidPermission", result.Error.Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task GetRolePermissionsHandler_fails_when_role_id_is_invalid_number(int invalidRoleId)
    {
        var repo = new FakeRolePermissionRepository();
        var handler = new GetRolePermissionsHandler(repo);

        var result = await handler.HandleAsync(new GetRolePermissionsQuery(invalidRoleId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.RoleNotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateRolePermissionsHandler_fails_when_permission_ids_is_null()
    {
        var repo = new FakeRolePermissionRepository();
        var lecturerRole = new Role { Id = 4, RoleName = nameof(SystemRoleName.Lecturer), Description = "Giảng viên" };
        repo.Roles.Add(lecturerRole);

        var handler = new UpdateRolePermissionsHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());
        var result = await handler.HandleAsync(new UpdateRolePermissionsCommand(4, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidPermission", result.Error.Code);
    }

    [Fact]
    public async Task UpdateRolePermissionsHandler_does_not_rotate_security_stamp_when_permissions_unchanged()
    {
        var repo = new FakeRolePermissionRepository();
        var perm1 = new Permission { Id = 1, PermissionCode = "identity.users.read", PermissionName = "Xem User", Module = "Identity" };
        var perm2 = new Permission { Id = 2, PermissionCode = "identity.users.manage", PermissionName = "Sửa User", Module = "Identity" };
        repo.Permissions.AddRange([perm1, perm2]);

        var lecturerRole = new Role { Id = 4, RoleName = nameof(SystemRoleName.Lecturer), Description = "Giảng viên" };
        repo.Roles.Add(lecturerRole);
        repo.RolePermissions.AddRange([
            new RolePermission { RoleId = 4, PermissionId = 1, Permission = perm1 },
            new RolePermission { RoleId = 4, PermissionId = 2, Permission = perm2 }
        ]);

        var user = User.Create(Guid.NewGuid(), "gv@ugtm.edu.vn", "GV@UGTM.EDU.VN", "GV A", "GV01", null, "hash", AccountStatus.Active, Now);
        var oldStamp = user.SecurityStamp;
        repo.UsersInRoles.Add(user);

        var auditLogs = new FakeAuditLogWriter();
        var handler = new UpdateRolePermissionsHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), auditLogs);
        var result = await handler.HandleAsync(new UpdateRolePermissionsCommand(4, [2, 1]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Permissions.Count);
        Assert.False(repo.SecurityStampRotated);
        Assert.Equal(oldStamp, user.SecurityStamp);
        Assert.Empty(auditLogs.Entries);
    }

    // --- CreateRoleHandler Tests ---

    [Fact]
    public async Task CreateRoleHandler_creates_role_without_permissions()
    {
        var repo = new FakeRolePermissionRepository();
        var auditLogs = new FakeAuditLogWriter();
        var handler = new CreateRoleHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), auditLogs);

        var command = new CreateRoleCommand("DepartmentHead", "Trưởng Bộ môn", null);
        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("DepartmentHead", result.Value.RoleName);
        Assert.Equal("Trưởng Bộ môn", result.Value.Description);
        Assert.Empty(result.Value.Permissions);
        Assert.True(result.Value.RoleId > 0);

        var audit = Assert.Single(auditLogs.Entries);
        Assert.Equal(AuditActions.RoleCreated, audit.Action);
        Assert.Equal("Role", audit.TargetType);
        Assert.Equal(result.Value.RoleId.ToString(), audit.TargetId);
    }

    [Fact]
    public async Task CreateRoleHandler_creates_role_with_permissions()
    {
        var repo = new FakeRolePermissionRepository();
        var perm1 = new Permission { Id = 1, PermissionCode = "identity.users.read", PermissionName = "Xem User", Module = "Identity" };
        var perm2 = new Permission { Id = 2, PermissionCode = "thesis.topics.read", PermissionName = "Xem Đề tài", Module = "ThesisManagement" };
        repo.Permissions.AddRange([perm1, perm2]);

        var auditLogs = new FakeAuditLogWriter();
        var handler = new CreateRoleHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), auditLogs);

        var command = new CreateRoleCommand("Dean", "Trưởng Khoa", [2, 1]);
        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Dean", result.Value.RoleName);
        Assert.Equal("Trưởng Khoa", result.Value.Description);
        Assert.Equal(2, result.Value.Permissions.Count);
        Assert.Equal("Identity", result.Value.Permissions[0].Module);
        Assert.Equal("ThesisManagement", result.Value.Permissions[1].Module);

        var audit = Assert.Single(auditLogs.Entries);
        Assert.Equal(AuditActions.RoleCreated, audit.Action);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateRoleHandler_fails_when_role_name_is_empty_or_whitespace(string? roleName)
    {
        var repo = new FakeRolePermissionRepository();
        var handler = new CreateRoleHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

        var command = new CreateRoleCommand(roleName!, "Desc", null);
        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidRoleName", result.Error.Code);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("ThisIsAVeryLongRoleNameThatExceedsTheFiftyCharacterLimitAllowedByTheSystem")]
    public async Task CreateRoleHandler_fails_when_role_name_length_invalid(string roleName)
    {
        var repo = new FakeRolePermissionRepository();
        var handler = new CreateRoleHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

        var command = new CreateRoleCommand(roleName, "Desc", null);
        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidRoleName", result.Error.Code);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("Role@#$!")]
    [InlineData("   123   ")]
    public async Task CreateRoleHandler_fails_when_role_name_pure_numeric_or_invalid_symbols(string roleName)
    {
        var repo = new FakeRolePermissionRepository();
        var handler = new CreateRoleHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

        var command = new CreateRoleCommand(roleName, "Desc", null);
        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidRoleName", result.Error.Code);
    }

    [Fact]
    public async Task CreateRoleHandler_fails_when_role_name_already_exists()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Roles.Add(new Role { Id = 4, RoleName = "Lecturer", Description = "Giảng viên" });

        var handler = new CreateRoleHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());
        var command = new CreateRoleCommand("lecturer", "Desc", null);
        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.RoleAlreadyExists", result.Error.Code);
    }

    [Fact]
    public async Task CreateRoleHandler_fails_when_permission_id_invalid()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Permissions.Add(new Permission { Id = 1, PermissionCode = "identity.users.read", PermissionName = "Xem User", Module = "Identity" });

        var handler = new CreateRoleHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());
        var command = new CreateRoleCommand("Reviewer", "Phản biện", [1, 999]);
        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidPermission", result.Error.Code);
    }

    [Fact]
    public async Task CreateRoleHandler_deduplicates_permission_ids()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Permissions.Add(new Permission { Id = 1, PermissionCode = "identity.users.read", PermissionName = "Xem User", Module = "Identity" });

        var handler = new CreateRoleHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());
        var command = new CreateRoleCommand("Reviewer", "Phản biện", [1, 1, 1]);
        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Permissions);
    }

    // --- GetRolesHandler Tests ---

    [Fact]
    public async Task GetRolesHandler_returns_only_visible_roles_by_default_and_orders_system_roles_first()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Roles.AddRange([
            new Role { Id = 8, RoleName = "HiddenRole", Description = "Hidden", IsHidden = true },
            new Role { Id = 7, RoleName = "DepartmentHead", Description = "Trưởng Bộ môn", IsHidden = false },
            new Role { Id = 4, RoleName = nameof(SystemRoleName.Lecturer), Description = "Giảng viên", IsHidden = false },
            new Role { Id = 1, RoleName = nameof(SystemRoleName.Admin), Description = "Quản trị viên", IsHidden = false }
        ]);
        repo.RolePermissions.Add(new RolePermission { RoleId = 7, PermissionId = 1 });
        repo.UserRoles.Add(new UserRole { RoleId = 4, UserId = Guid.NewGuid() });

        var handler = new GetRolesHandler(repo);
        var result = await handler.HandleAsync(new GetRolesQuery(IncludeHidden: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal(["Admin", "Lecturer", "DepartmentHead"], result.Value.Select(r => r.RoleName).ToArray());
        Assert.True(result.Value[0].IsSystemRole);
        Assert.True(result.Value[1].IsSystemRole);
        Assert.False(result.Value[2].IsSystemRole);
        Assert.Equal(1, result.Value[1].UserCount);
        Assert.Equal(1, result.Value[2].PermissionCount);
    }

    [Fact]
    public async Task GetRolesHandler_returns_all_roles_when_include_hidden_is_true()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Roles.AddRange([
            new Role { Id = 1, RoleName = nameof(SystemRoleName.Admin), Description = "Admin", IsHidden = false },
            new Role { Id = 7, RoleName = "UnusedCustom", Description = "Custom", IsHidden = true }
        ]);

        var handler = new GetRolesHandler(repo);
        var result = await handler.HandleAsync(new GetRolesQuery(IncludeHidden: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, r => r.RoleName == "UnusedCustom" && r.IsHidden && !r.CanHide);
    }

    [Fact]
    public async Task GetRolesHandler_computes_can_hide_correctly_based_on_system_role_user_count_and_hidden_state()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Roles.AddRange([
            new Role { Id = 1, RoleName = nameof(SystemRoleName.Admin), Description = "Admin", IsHidden = false },
            new Role { Id = 7, RoleName = "UsedCustom", Description = "Used", IsHidden = false },
            new Role { Id = 8, RoleName = "UnusedCustom", Description = "Unused", IsHidden = false },
            new Role { Id = 9, RoleName = "HiddenCustom", Description = "Hidden", IsHidden = true }
        ]);
        repo.UserRoles.Add(new UserRole { RoleId = 7, UserId = Guid.NewGuid() });

        var handler = new GetRolesHandler(repo);
        var result = await handler.HandleAsync(new GetRolesQuery(IncludeHidden: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var byName = result.Value.ToDictionary(r => r.RoleName);
        Assert.False(byName["Admin"].CanHide);
        Assert.False(byName["UsedCustom"].CanHide);
        Assert.True(byName["UnusedCustom"].CanHide);
        Assert.False(byName["HiddenCustom"].CanHide);
    }

    // --- UpdateRoleVisibilityHandler Tests ---

    [Fact]
    public async Task UpdateRoleVisibilityHandler_hides_unused_custom_role_and_writes_audit_log()
    {
        var repo = new FakeRolePermissionRepository();
        var customRole = new Role { Id = 7, RoleName = "DepartmentHead", Description = "Trưởng Bộ môn", IsHidden = false };
        repo.Roles.Add(customRole);
        repo.RolePermissions.Add(new RolePermission { RoleId = 7, PermissionId = 1 });

        var auditLogs = new FakeAuditLogWriter();
        var handler = new UpdateRoleVisibilityHandler(repo, new DirectUnitOfWork(), auditLogs);
        var result = await handler.HandleAsync(new UpdateRoleVisibilityCommand(7, IsHidden: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsHidden);
        Assert.False(result.Value.IsSystemRole);
        Assert.False(result.Value.CanHide);
        Assert.Equal(0, result.Value.UserCount);
        Assert.Equal(1, result.Value.PermissionCount);
        Assert.True(customRole.IsHidden);

        var audit = Assert.Single(auditLogs.Entries);
        Assert.Equal(AuditActions.RoleHidden, audit.Action);
        Assert.Equal("Role", audit.TargetType);
        Assert.Equal("7", audit.TargetId);
    }

    [Theory]
    [InlineData(SystemRoleName.Admin)]
    [InlineData(SystemRoleName.Faculty)]
    [InlineData(SystemRoleName.AcademicAffairs)]
    [InlineData(SystemRoleName.Lecturer)]
    [InlineData(SystemRoleName.Student)]
    [InlineData(SystemRoleName.CouncilMember)]
    public async Task UpdateRoleVisibilityHandler_fails_when_hiding_system_role(SystemRoleName systemRole)
    {
        var repo = new FakeRolePermissionRepository();
        repo.Roles.Add(new Role { Id = (int)systemRole + 1, RoleName = systemRole.ToString(), IsHidden = false });

        var handler = new UpdateRoleVisibilityHandler(repo, new DirectUnitOfWork(), new FakeAuditLogWriter());
        var result = await handler.HandleAsync(new UpdateRoleVisibilityCommand((int)systemRole + 1, IsHidden: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.CannotHideSystemRole.Code, result.Error.Code);
    }

    [Fact]
    public async Task UpdateRoleVisibilityHandler_fails_when_hiding_role_in_use_by_users()
    {
        var repo = new FakeRolePermissionRepository();
        var customRole = new Role { Id = 7, RoleName = "DepartmentHead", IsHidden = false };
        repo.Roles.Add(customRole);
        repo.UserRoles.Add(new UserRole { RoleId = 7, UserId = Guid.NewGuid() });

        var handler = new UpdateRoleVisibilityHandler(repo, new DirectUnitOfWork(), new FakeAuditLogWriter());
        var result = await handler.HandleAsync(new UpdateRoleVisibilityCommand(7, IsHidden: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.RoleInUse.Code, result.Error.Code);
        Assert.False(customRole.IsHidden);
    }

    [Fact]
    public async Task UpdateRoleVisibilityHandler_fails_when_hiding_already_hidden_role()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Roles.Add(new Role { Id = 7, RoleName = "DepartmentHead", IsHidden = true });

        var handler = new UpdateRoleVisibilityHandler(repo, new DirectUnitOfWork(), new FakeAuditLogWriter());
        var result = await handler.HandleAsync(new UpdateRoleVisibilityCommand(7, IsHidden: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.RoleAlreadyHidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task UpdateRoleVisibilityHandler_unhides_hidden_role_and_writes_audit_log()
    {
        var repo = new FakeRolePermissionRepository();
        var customRole = new Role { Id = 7, RoleName = "DepartmentHead", IsHidden = true };
        repo.Roles.Add(customRole);

        var auditLogs = new FakeAuditLogWriter();
        var handler = new UpdateRoleVisibilityHandler(repo, new DirectUnitOfWork(), auditLogs);
        var result = await handler.HandleAsync(new UpdateRoleVisibilityCommand(7, IsHidden: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsHidden);
        Assert.True(result.Value.CanHide);
        Assert.False(customRole.IsHidden);

        var audit = Assert.Single(auditLogs.Entries);
        Assert.Equal(AuditActions.RoleUnhidden, audit.Action);
        Assert.Equal("7", audit.TargetId);
    }

    [Fact]
    public async Task UpdateRoleVisibilityHandler_fails_when_unhiding_visible_role()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Roles.Add(new Role { Id = 7, RoleName = "DepartmentHead", IsHidden = false });

        var handler = new UpdateRoleVisibilityHandler(repo, new DirectUnitOfWork(), new FakeAuditLogWriter());
        var result = await handler.HandleAsync(new UpdateRoleVisibilityCommand(7, IsHidden: false), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.RoleNotHidden.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(999)]
    public async Task UpdateRoleVisibilityHandler_fails_when_role_not_found_or_invalid_id(int roleId)
    {
        var repo = new FakeRolePermissionRepository();
        var handler = new UpdateRoleVisibilityHandler(repo, new DirectUnitOfWork(), new FakeAuditLogWriter());

        var result = await handler.HandleAsync(new UpdateRoleVisibilityCommand(roleId, IsHidden: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.RoleNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task UpdateRoleVisibilityHandler_fails_when_is_hidden_is_null()
    {
        var repo = new FakeRolePermissionRepository();
        var handler = new UpdateRoleVisibilityHandler(repo, new DirectUnitOfWork(), new FakeAuditLogWriter());

        var result = await handler.HandleAsync(new UpdateRoleVisibilityCommand(7, IsHidden: null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact]
    public async Task UpdateRolePermissionsHandler_fails_when_role_is_hidden()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Permissions.Add(new Permission { Id = 1, PermissionCode = "identity.users.read", Module = "Identity" });
        repo.Roles.Add(new Role { Id = 7, RoleName = "DepartmentHead", IsHidden = true });

        var handler = new UpdateRolePermissionsHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());
        var result = await handler.HandleAsync(new UpdateRolePermissionsCommand(7, [1]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.RoleIsHidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task UpdateDescription_normalizes_and_audits_changes_including_system_roles()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Roles.Add(new Role { Id = 4, RoleName = "Lecturer", Description = "Old" });
        var audit = new FakeAuditLogWriter();
        var handler = new UpdateRoleDescriptionHandler(repo, new DirectUnitOfWork(), audit);
        var result = await handler.HandleAsync(4, "  Mô tả mới  ", CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("Mô tả mới", result.Value.Description);
        Assert.Equal(AuditActions.RoleDescriptionChanged, Assert.Single(audit.Entries).Action);
        var unchanged = await handler.HandleAsync(4, "Mô tả mới", CancellationToken.None);
        Assert.True(unchanged.IsSuccess);
        Assert.Single(audit.Entries);
        var cleared = await handler.HandleAsync(4, "   ", CancellationToken.None);
        Assert.Null(cleared.Value.Description);
        Assert.Equal(2, audit.Entries.Count);
    }

    [Fact]
    public async Task UpdateDescription_rejects_too_long_and_unknown_role()
    {
        var repo = new FakeRolePermissionRepository();
        repo.Roles.Add(new Role { Id = 7, RoleName = "Reviewer", Description = "Old" });
        var handler = new UpdateRoleDescriptionHandler(repo, new DirectUnitOfWork(), new FakeAuditLogWriter());
        var tooLong = await handler.HandleAsync(7, new string('x', 256), CancellationToken.None);
        Assert.Equal(IdentityErrors.InvalidInput.Code, tooLong.Error.Code);
        Assert.Equal("Old", repo.Roles[0].Description);
        var missing = await handler.HandleAsync(99, "new", CancellationToken.None);
        Assert.Equal(IdentityErrors.RoleNotFound.Code, missing.Error.Code);
        Assert.True((await handler.HandleAsync(7, new string('x', 255), CancellationToken.None)).IsSuccess);
    }

    private sealed class FakeRolePermissionRepository : IRolePermissionRepository
    {
        public List<Permission> Permissions { get; } = [];
        public List<Role> Roles { get; } = [];
        public List<RolePermission> RolePermissions { get; } = [];
        public List<UserRole> UserRoles { get; } = [];
        public List<User> UsersInRoles { get; } = [];
        public bool SecurityStampRotated { get; private set; }

        public Task<IReadOnlyList<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Permission>>(Permissions);

        public Task<IReadOnlyList<RoleWithCounts>> GetAllRolesWithCountsAsync(bool includeHidden, CancellationToken cancellationToken)
        {
            var query = includeHidden ? Roles : Roles.Where(r => !r.IsHidden);
            var list = query
                .OrderBy(r => r.Id)
                .Select(r => new RoleWithCounts(
                    r,
                    UserRoles.Count(ur => ur.RoleId == r.Id),
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
            => Task.FromResult(UserRoles.Count(ur => ur.RoleId == roleId));

        public Task<int> GetRolePermissionCountAsync(int roleId, CancellationToken cancellationToken)
            => Task.FromResult(RolePermissions.Count(rp => rp.RoleId == roleId));

        public Task<bool> RoleExistsAsync(int roleId, CancellationToken cancellationToken)
            => Task.FromResult(Roles.Any(r => r.Id == roleId));

        public Task<bool> RoleNameExistsAsync(string roleName, CancellationToken cancellationToken)
            => Task.FromResult(Roles.Any(r => string.Equals(r.RoleName, roleName, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<Permission>> GetPermissionsByIdsAsync(IEnumerable<int> permissionIds, CancellationToken cancellationToken)
        {
            var idSet = permissionIds.ToHashSet();
            var list = Permissions.Where(p => idSet.Contains(p.Id)).ToList();
            return Task.FromResult<IReadOnlyList<Permission>>(list);
        }

        public Task<Role> CreateRoleAsync(string roleName, string? description, IEnumerable<int> permissionIds, DateTime assignedAt, CancellationToken cancellationToken)
        {
            var nextId = Roles.Count > 0 ? Roles.Max(r => r.Id) + 1 : 1;
            var role = new Role
            {
                Id = nextId,
                RoleName = roleName,
                Description = description
            };
            Roles.Add(role);
            foreach (var pid in permissionIds.Distinct())
            {
                RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid, AssignedAt = assignedAt });
            }
            return Task.FromResult(role);
        }

        public Task UpdateRolePermissionsAsync(int roleId, IEnumerable<int> permissionIds, DateTime assignedAt, CancellationToken cancellationToken)
        {
            RolePermissions.RemoveAll(rp => rp.RoleId == roleId);
            foreach (var pid in permissionIds)
            {
                RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = pid, AssignedAt = assignedAt });
            }
            return Task.CompletedTask;
        }

        public Task RotateSecurityStampForRoleUsersAsync(int roleId, CancellationToken cancellationToken)
        {
            SecurityStampRotated = true;
            foreach (var user in UsersInRoles)
            {
                user.RotateSecurityStamp();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class DirectUnitOfWork : IIdentityUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class FakeClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    private sealed class FakeAuditLogWriter : IAuditLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public void Add(AuditEntry entry) => Entries.Add(entry);
    }
}
