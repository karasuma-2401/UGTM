using UGTM.Modules.Identity.Application.Permissions.Dtos;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Permissions.GetPermissions;

public sealed class GetPermissionsHandler(IRolePermissionRepository repository)
{
    public async Task<Result<IReadOnlyList<ModulePermissionsDto>>> HandleAsync(
        GetPermissionsQuery query,
        CancellationToken cancellationToken)
    {
        var permissions = await repository.GetAllPermissionsAsync(cancellationToken);

        var grouped = permissions
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Module) ? "General" : p.Module.Trim())
            .OrderBy(g => g.Key)
            .Select(g => new ModulePermissionsDto(
                g.Key,
                g.OrderBy(p => p.PermissionCode)
                    .Select(p => new PermissionDto(p.Id, p.PermissionCode, p.PermissionName, p.Module))
                    .ToList()))
            .ToList();

        return Result.Success<IReadOnlyList<ModulePermissionsDto>>(grouped);
    }
}
