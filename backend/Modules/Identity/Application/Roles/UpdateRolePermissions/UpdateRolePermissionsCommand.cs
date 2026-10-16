namespace UGTM.Modules.Identity.Application.Roles.UpdateRolePermissions;

public sealed record UpdateRolePermissionsCommand(
    int RoleId,
    IReadOnlyList<int>? PermissionIds);
