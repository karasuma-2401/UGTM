namespace UGTM.Modules.Identity.Application.Roles.CreateRole;

public sealed record CreateRoleCommand(
    string RoleName,
    string? Description,
    IReadOnlyList<int>? PermissionIds);
