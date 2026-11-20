namespace UGTM.Modules.Identity.Application.Roles.UpdateRoleVisibility;

/// <summary>Lệnh ẩn hoặc hiển thị lại một vai trò trong hệ thống.</summary>
public sealed record UpdateRoleVisibilityCommand(int RoleId, bool? IsHidden);
