namespace UGTM.Modules.Identity.Application.Permissions.Dtos;

public sealed record ModulePermissionsDto(
    string Module,
    IReadOnlyList<PermissionDto> Permissions);
