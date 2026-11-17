using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Permissions.GetPermissions;
using UGTM.Modules.Identity.Application.Roles.CreateRole;
using UGTM.Modules.Identity.Application.Roles.GetRolePermissions;
using UGTM.Modules.Identity.Application.Roles.UpdateRolePermissions;
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

    private sealed class FakeRolePermissionRepository : IRolePermissionRepository
    {
        public List<Permission> Permissions { get; } = [];
        public List<Role> Roles { get; } = [];
        public List<RolePermission> RolePermissions { get; } = [];
        public List<User> UsersInRoles { get; } = [];
        public bool SecurityStampRotated { get; private set; }

        public Task<IReadOnlyList<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Permission>>(Permissions);

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
