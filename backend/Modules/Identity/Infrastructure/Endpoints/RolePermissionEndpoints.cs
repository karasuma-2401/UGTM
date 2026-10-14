using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.Modules.Identity.Application.Permissions.GetPermissions;
using UGTM.Modules.Identity.Application.Roles.GetRolePermissions;
using UGTM.Modules.Identity.Application.Roles.UpdateRolePermissions;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class RolePermissionEndpoints
{
    public sealed record UpdateRolePermissionsRequest(IReadOnlyList<int> PermissionIds);

    public static IEndpointRouteBuilder MapIdentityRolePermissionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        // 1. GET /api/permissions
        endpoints.MapGroup("/api/permissions")
            .WithTags("Identity.Permissions")
            .MapGet("/", GetPermissionsAsync)
            .WithSummary("Lấy danh mục quyền trong toàn hệ thống theo từng Module")
            .WithDescription("""
                Trả về danh sách tất cả quyền trong hệ thống được gom nhóm theo từng Module (Identity, ThesisManagement, v.v.).
                Dùng cho giao diện phân quyền vai trò người dùng (RBAC / PBAC).

                Yêu cầu Bearer token có quyền `identity.permissions.read` hoặc `identity.roles.manage`.
                Thiếu token hoặc token không hợp lệ: 401; không đủ quyền: 403.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityPermissionsRead}");

        // 2. Roles group: /api/roles
        var rolesGroup = endpoints.MapGroup("/api/roles")
            .WithTags("Identity.Roles");

        rolesGroup.MapGet("/{id:int}/permissions", GetRolePermissionsAsync)
            .WithSummary("Lấy thông tin vai trò và danh sách quyền hiện tại")
            .WithDescription("""
                Lấy thông tin vai trò (Role) và danh sách các quyền (Permissions) hiện đang được cấp cho vai trò này.

                Yêu cầu Bearer token có quyền `identity.roles.manage` hoặc `identity.permissions.read`.
                Không tìm thấy vai trò: 404 `Identity.RoleNotFound`.
                Thiếu token hoặc token không hợp lệ: 401; không đủ quyền: 403.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityRolesManage}");

        rolesGroup.MapPut("/{id:int}/permissions", UpdateRolePermissionsAsync)
            .WithSummary("Cập nhật danh sách quyền cho vai trò (Role)")
            .WithDescription("""
                Cập nhật lại toàn bộ danh sách quyền được cấp cho một vai trò.
                Khi phân quyền thay đổi, hệ thống sẽ tự động xoay Security Stamp cho tất cả người dùng thuộc vai trò này
                để thu hồi Access Token cũ và cập nhật claims quyền mới ở phiên kế tiếp.

                Yêu cầu Bearer token có quyền `identity.roles.manage`.
                Danh sách quyền không hợp lệ: 400 `Identity.InvalidPermission`;
                không thể gỡ sạch quyền của Admin: 400 `Identity.CannotRevokeAllAdminPermissions`;
                không tìm thấy vai trò: 404 `Identity.RoleNotFound`.
                Thiếu token hoặc token không hợp lệ: 401; không đủ quyền: 403.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityRolesManage}");

        return endpoints;
    }

    private static async Task<IResult> GetPermissionsAsync(
        GetPermissionsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetPermissionsQuery(), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetRolePermissionsAsync(
        [FromRoute] int id,
        GetRolePermissionsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetRolePermissionsQuery(id), cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UpdateRolePermissionsAsync(
        [FromRoute] int id,
        [FromBody] UpdateRolePermissionsRequest request,
        UpdateRolePermissionsHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRolePermissionsCommand(id, request.PermissionIds);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }
}
