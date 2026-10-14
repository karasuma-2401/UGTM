using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Domain.Repositories;

public interface IRolePermissionRepository
{
    Task<IReadOnlyList<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken);
    Task<Role?> GetRoleWithPermissionsAsync(int roleId, CancellationToken cancellationToken);
    Task<bool> RoleExistsAsync(int roleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Permission>> GetPermissionsByIdsAsync(IEnumerable<int> permissionIds, CancellationToken cancellationToken);
    Task UpdateRolePermissionsAsync(int roleId, IEnumerable<int> permissionIds, DateTime assignedAt, CancellationToken cancellationToken);
    Task RotateSecurityStampForRoleUsersAsync(int roleId, CancellationToken cancellationToken);
}
