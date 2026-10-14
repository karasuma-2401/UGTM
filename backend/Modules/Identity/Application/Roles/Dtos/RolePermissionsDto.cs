using UGTM.Modules.Identity.Application.Permissions.Dtos;

namespace UGTM.Modules.Identity.Application.Roles.Dtos;

public sealed record RolePermissionsDto(
    int RoleId,
    string RoleName,
    string? Description,
    IReadOnlyList<PermissionDto> Permissions);
