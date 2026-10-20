using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.Permissions.Dtos;
using UGTM.Modules.Identity.Application.Permissions.GetPermissions;
using UGTM.Modules.Identity.Application.Roles.CreateRole;
using UGTM.Modules.Identity.Application.Roles.Dtos;
using UGTM.Modules.Identity.Application.Roles.GetRolePermissions;
using UGTM.Modules.Identity.Application.Roles.UpdateRolePermissions;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class RolePermissionEndpoints
{
    public sealed record CreateRoleRequest(
        string RoleName,
        string? Description,
        IReadOnlyList<int>? PermissionIds);

    public sealed record UpdateRolePermissionsRequest(IReadOnlyList<int>? PermissionIds);

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

                Yêu cầu Bearer token có quyền `identity.permissions.read` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm danh mục quyền gom nhóm theo từng Module.
                - Thiếu token hoặc token không hợp lệ: 401 Unauthorized.
                - Không đủ quyền (không có quyền `identity.permissions.read` và không phải Admin): 403 Forbidden.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityPermissionsRead}")
            .Produces<ApiResponse<IReadOnlyList<ModulePermissionsDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // 2. Roles group: /api/roles
        var rolesGroup = endpoints.MapGroup("/api/roles")
            .WithTags("Identity.Roles");

        rolesGroup.MapPost("/", CreateRoleAsync)
            .WithSummary("Tạo vai trò người dùng mới")
            .WithDescription("""
                Tạo vai trò người dùng mới (Role) trong hệ thống với tên vai trò, mô tả và danh sách quyền ban đầu (tùy chọn).
                Tên vai trò phải là duy nhất (không phân biệt chữ hoa, chữ thường), có độ dài từ 2 đến 50 ký tự và không được để trống.
                Nếu danh sách `permissionIds` được cung cấp, các mã quyền tương ứng sẽ được gán ngay cho vai trò vừa tạo (tự động loại bỏ ID trùng lặp).
                Nếu có bất kỳ `permissionId` nào không tồn tại trong hệ thống, yêu cầu sẽ bị từ chối với mã lỗi 400 Bad Request (`Identity.InvalidPermission`).

                Yêu cầu Bearer token có quyền `identity.roles.manage` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm thông tin vai trò vừa tạo và danh sách quyền được cấp (`RolePermissionsDto`).
                - Dữ liệu không hợp lệ (tên vai trò trống, sai độ dài, chứa ký tự không hợp lệ): 400 Bad Request (`Identity.InvalidRoleName`).
                - Danh sách quyền chứa ID không tồn tại: 400 Bad Request (`Identity.InvalidPermission`).
                - Vai trò đã tồn tại (trùng `roleName`): 409 Conflict (`Identity.RoleAlreadyExists`).
                - Thiếu token hoặc token không hợp lệ: 401 Unauthorized.
                - Không đủ quyền: 403 Forbidden.
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityRolesManage}")
            .Produces<ApiResponse<RolePermissionsDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        rolesGroup.MapGet("/{id:int}/permissions", GetRolePermissionsAsync)
            .WithSummary("Lấy thông tin vai trò và danh sách quyền hiện tại")
            .WithDescription("""
                Lấy thông tin vai trò (Role) và danh sách các quyền (Permissions) hiện đang được cấp cho vai trò này.
                Tham số `id` là Role ID tương ứng trong hệ thống:
                - 1: Admin
                - 2: Faculty (Ban chủ nhiệm Khoa)
                - 3: AcademicAffairs (Phòng Đào tạo)
                - 4: Lecturer (Giảng viên)
                - 5: Student (Sinh viên)
                - 6: CouncilMember (Thành viên Hội đồng)

                Yêu cầu Bearer token có quyền `identity.roles.manage` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm thông tin vai trò và danh sách quyền.
                - Thiếu token hoặc token không hợp lệ: 401 Unauthorized.
                - Không đủ quyền: 403 Forbidden.
                - Không tìm thấy vai trò: 404 Not Found (`Identity.RoleNotFound`).
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityRolesManage}")
            .Produces<ApiResponse<RolePermissionsDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        rolesGroup.MapPut("/{id:int}/permissions", UpdateRolePermissionsAsync)
            .WithSummary("Cập nhật danh sách quyền cho vai trò (Role)")
            .WithDescription("""
                Cập nhật lại toàn bộ danh sách quyền được cấp cho một vai trò (đối soát và đồng bộ).
                Hệ thống tự động loại bỏ các ID trùng lặp trong danh sách `permissionIds`.
                Khi phân quyền thực sự thay đổi, hệ thống sẽ tự động xoay Security Stamp cho tất cả người dùng thuộc vai trò này
                để thu hồi phiên đăng nhập hiện tại và cập nhật claims quyền mới ở lần refresh token kế tiếp.

                Yêu cầu Bearer token có quyền `identity.roles.manage` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm thông tin vai trò và danh sách quyền sau khi cập nhật.
                - Danh sách quyền là `null` hoặc chứa ID không tồn tại: 400 Bad Request (`Identity.InvalidPermission`).
                - Cố tình gỡ sạch quyền của Admin: 400 Bad Request (`Identity.CannotRevokeAllAdminPermissions`).
                - Thiếu token hoặc token không hợp lệ: 401 Unauthorized.
                - Không đủ quyền: 403 Forbidden.
                - Không tìm thấy vai trò: 404 Not Found (`Identity.RoleNotFound`).
                """)
            .RequireAuthorization($"Permission:{PermissionCodes.IdentityRolesManage}")
            .Produces<ApiResponse<RolePermissionsDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateRoleAsync(
        [FromBody] CreateRoleRequest? request,
        CreateRoleHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateRoleCommand(
            request?.RoleName ?? string.Empty,
            request?.Description,
            request?.PermissionIds);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
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
        [FromBody] UpdateRolePermissionsRequest? request,
        UpdateRolePermissionsHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRolePermissionsCommand(id, request?.PermissionIds);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }
}
