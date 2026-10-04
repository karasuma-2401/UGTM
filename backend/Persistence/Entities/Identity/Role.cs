using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Entities.Identity;

public class Role
{
    public int Id { get; set; }
    public SystemRoleName RoleName { get; set; }
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
