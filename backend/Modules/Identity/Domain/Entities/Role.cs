namespace UGTM.Modules.Identity.Domain.Entities;

public class Role
{
    public int Id { get; set; }
    public string RoleName { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
