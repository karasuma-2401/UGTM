using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Domain.Entities;

public class Role
{
    public int Id { get; set; }
    public string RoleName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsHidden { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];

    public bool IsSystemRole() => IsSystemRoleName(RoleName);

    public static bool IsSystemRoleName(string? roleName) =>
        !string.IsNullOrWhiteSpace(roleName)
        && !int.TryParse(roleName.Trim(), out _)
        && Enum.TryParse<SystemRoleName>(roleName.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed);
}
