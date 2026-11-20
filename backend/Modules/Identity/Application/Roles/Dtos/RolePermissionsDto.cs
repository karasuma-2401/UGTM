using UGTM.Modules.Identity.Application.Permissions.Dtos;

namespace UGTM.Modules.Identity.Application.Roles.Dtos;

public sealed record RoleSummaryDto(
    int RoleId,
    string RoleName,
    string? Description,
    bool IsSystemRole,
    bool IsHidden,
    int UserCount,
    int PermissionCount,
    bool CanHide);

public sealed record RolePermissionsDto(
    int RoleId,
    string RoleName,
    string? Description,
    IReadOnlyList<PermissionDto> Permissions,
    bool IsSystemRole = false,
    bool IsHidden = false,
    int UserCount = 0);
