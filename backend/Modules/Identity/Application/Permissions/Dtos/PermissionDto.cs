namespace UGTM.Modules.Identity.Application.Permissions.Dtos;

public sealed record PermissionDto(
    int Id,
    string PermissionCode,
    string? PermissionName,
    string? Module);
