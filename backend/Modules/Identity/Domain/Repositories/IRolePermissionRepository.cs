using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Domain.Repositories;

public sealed record RoleWithCounts(Role Role, int UserCount, int PermissionCount);

public interface IRolePermissionRepository
{
    Task<IReadOnlyList<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RoleWithCounts>> GetAllRolesWithCountsAsync(bool includeHidden, CancellationToken cancellationToken);
    Task<Role?> GetRoleWithPermissionsAsync(int roleId, CancellationToken cancellationToken);
    Task<Role?> GetRoleByIdForUpdateAsync(int roleId, CancellationToken cancellationToken);
    Task<int> GetRoleUserCountAsync(int roleId, CancellationToken cancellationToken);
    Task<int> GetRolePermissionCountAsync(int roleId, CancellationToken cancellationToken);
    Task<bool> RoleExistsAsync(int roleId, CancellationToken cancellationToken);
    Task<bool> RoleNameExistsAsync(string roleName, CancellationToken cancellationToken);
    Task<IReadOnlyList<Permission>> GetPermissionsByIdsAsync(IEnumerable<int> permissionIds, CancellationToken cancellationToken);
    Task<Role> CreateRoleAsync(string roleName, string? description, IEnumerable<int> permissionIds, DateTime assignedAt, CancellationToken cancellationToken);
    Task UpdateRolePermissionsAsync(int roleId, IEnumerable<int> permissionIds, DateTime assignedAt, CancellationToken cancellationToken);
    Task RotateSecurityStampForRoleUsersAsync(int roleId, CancellationToken cancellationToken);
}
