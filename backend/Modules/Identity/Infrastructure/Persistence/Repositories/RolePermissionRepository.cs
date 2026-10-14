using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class RolePermissionRepository(IdentityDbContext db) : IRolePermissionRepository
{
    public async Task<IReadOnlyList<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken)
    {
        return await db.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Module)
            .ThenBy(p => p.PermissionCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<Role?> GetRoleWithPermissionsAsync(int roleId, CancellationToken cancellationToken)
    {
        return await db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
    }

    public async Task<bool> RoleExistsAsync(int roleId, CancellationToken cancellationToken)
    {
        return await db.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == roleId, cancellationToken);
    }

    public async Task<IReadOnlyList<Permission>> GetPermissionsByIdsAsync(
        IEnumerable<int> permissionIds,
        CancellationToken cancellationToken)
    {
        var idList = permissionIds.ToList();
        return await db.Permissions
            .AsNoTracking()
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateRolePermissionsAsync(
        int roleId,
        IEnumerable<int> permissionIds,
        DateTime assignedAt,
        CancellationToken cancellationToken)
    {
        var currentRolePermissions = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);

        var targetIds = permissionIds.ToHashSet();

        // 1. Gỡ bỏ quyền không còn được chọn
        var toRemove = currentRolePermissions
            .Where(rp => !targetIds.Contains(rp.PermissionId))
            .ToList();

        if (toRemove.Count > 0)
        {
            db.RolePermissions.RemoveRange(toRemove);
        }

        // 2. Thêm quyền mới
        var existingIds = currentRolePermissions
            .Select(rp => rp.PermissionId)
            .ToHashSet();

        var toAdd = targetIds
            .Where(id => !existingIds.Contains(id))
            .ToList();

        if (toAdd.Count > 0)
        {
            foreach (var permId in toAdd)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permId,
                    AssignedAt = assignedAt
                });
            }
        }
    }

    public async Task RotateSecurityStampForRoleUsersAsync(int roleId, CancellationToken cancellationToken)
    {
        var users = await db.UserRoles
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.User)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            user.RotateSecurityStamp();
        }
    }
}
