using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Permissions.Dtos;
using UGTM.Modules.Identity.Application.Roles.Dtos;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Roles.GetRolePermissions;

public sealed class GetRolePermissionsHandler(IRolePermissionRepository repository)
{
    public async Task<Result<RolePermissionsDto>> HandleAsync(
        GetRolePermissionsQuery query,
        CancellationToken cancellationToken)
    {
        var role = await repository.GetRoleWithPermissionsAsync(query.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure<RolePermissionsDto>(IdentityErrors.RoleNotFound);
        }

        var permissions = role.RolePermissions
            .Select(rp => new PermissionDto(
                rp.Permission.Id,
                rp.Permission.PermissionCode,
                rp.Permission.PermissionName,
                rp.Permission.Module))
            .OrderBy(p => p.Module)
            .ThenBy(p => p.PermissionCode)
            .ToList();

        return Result.Success(new RolePermissionsDto(
            role.Id,
            role.RoleName.ToString(),
            role.Description,
            permissions));
    }
}
