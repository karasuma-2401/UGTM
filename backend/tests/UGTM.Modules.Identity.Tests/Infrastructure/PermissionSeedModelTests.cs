using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Infrastructure.Persistence.Configurations;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class PermissionSeedModelTests
{
    [Fact]
    public void PermissionConfiguration_seeds_all_six_required_permissions()
    {
        var modelBuilder = new ModelBuilder();
        new PermissionConfiguration().Configure(modelBuilder.Entity<Permission>());

        var permissionEntity = modelBuilder.Model.FindEntityType(typeof(Permission));
        Assert.NotNull(permissionEntity);

        var seedPermissions = permissionEntity.GetSeedData().ToList();
        Assert.Equal(6, seedPermissions.Count);

        var permissionCodes = seedPermissions
            .Select(p => p[nameof(Permission.PermissionCode)]?.ToString())
            .ToList();

        Assert.Contains(PermissionCodes.IdentityUsersRead, permissionCodes);
        Assert.Contains(PermissionCodes.IdentityUsersManage, permissionCodes);
        Assert.Contains(PermissionCodes.IdentityUsersImport, permissionCodes);
        Assert.Contains(PermissionCodes.IdentityPermissionsRead, permissionCodes);
        Assert.Contains(PermissionCodes.IdentityRolesManage, permissionCodes);
        Assert.Contains(PermissionCodes.IdentitySessionsRevoke, permissionCodes);

        Assert.All(seedPermissions, p =>
        {
            Assert.Equal("Identity", p[nameof(Permission.Module)]);
            Assert.False(string.IsNullOrWhiteSpace(p[nameof(Permission.PermissionName)]?.ToString()));
        });
    }

    [Fact]
    public void RolePermissionConfiguration_seeds_default_role_permissions_for_Admin_Faculty_and_AcademicAffairs()
    {
        var modelBuilder = new ModelBuilder();
        new RolePermissionConfiguration().Configure(modelBuilder.Entity<RolePermission>());

        var rolePermissionEntity = modelBuilder.Model.FindEntityType(typeof(RolePermission));
        Assert.NotNull(rolePermissionEntity);

        var seedRolePermissions = rolePermissionEntity.GetSeedData().ToList();

        // Admin (Role ID = 1) must have all 6 permissions
        var adminPermIds = seedRolePermissions
            .Where(rp => (int)rp[nameof(RolePermission.RoleId)]! == 1)
            .Select(rp => (int)rp[nameof(RolePermission.PermissionId)]!)
            .Order()
            .ToList();
        Assert.Equal([1, 2, 3, 4, 5, 6], adminPermIds);

        // Faculty (Role ID = 2) must have permissions 1, 3 (identity.users.read, identity.users.import)
        var facultyPermIds = seedRolePermissions
            .Where(rp => (int)rp[nameof(RolePermission.RoleId)]! == 2)
            .Select(rp => (int)rp[nameof(RolePermission.PermissionId)]!)
            .Order()
            .ToList();
        Assert.Equal([1, 3], facultyPermIds);

        // AcademicAffairs (Role ID = 3) must have permissions 1, 2, 3, 4 (users.read, users.manage, users.import, permissions.read)
        var aaPermIds = seedRolePermissions
            .Where(rp => (int)rp[nameof(RolePermission.RoleId)]! == 3)
            .Select(rp => (int)rp[nameof(RolePermission.PermissionId)]!)
            .Order()
            .ToList();
        Assert.Equal([1, 2, 3, 4], aaPermIds);
    }
}
