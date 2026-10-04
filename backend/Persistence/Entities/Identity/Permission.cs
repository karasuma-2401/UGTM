namespace UGTM.Persistence.Entities.Identity;

public class Permission
{
    public int Id { get; set; }
    public string PermissionCode { get; set; } = null!;
    public string? PermissionName { get; set; }
    public string? Module { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
