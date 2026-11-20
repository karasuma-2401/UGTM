namespace UGTM.Modules.Identity.Application.Roles.GetRoles;

/// <summary>Truy vấn danh sách các vai trò trong hệ thống.</summary>
public sealed record GetRolesQuery(bool IncludeHidden = false);
