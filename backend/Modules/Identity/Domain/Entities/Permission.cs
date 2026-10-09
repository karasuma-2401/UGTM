namespace UGTM.Modules.Identity.Domain.Entities;

public class Permission
{
    public int Id { get; set; }
    public string PermissionCode { get; set; } = null!;
    public string? PermissionName { get; set; }
    public string? Module { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
