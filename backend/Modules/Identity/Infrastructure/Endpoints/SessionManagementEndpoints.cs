using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.Sessions;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Infrastructure.Authentication;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class SessionManagementEndpoints
{
    public static IEndpointRouteBuilder MapIdentitySessionManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users/{userId:guid}/sessions")
            .WithTags("Identity.Sessions")
            .RequireAuthorization($"Permission:{PermissionCodes.IdentitySessionsRevoke}");

        group.MapGet("/", GetUserSessionsAsync)
            .WithSummary("Tra cứu phiên đăng nhập của người dùng")
            .WithDescription("""
                Trả danh sách phiên đăng nhập còn hiệu lực của một tài khoản, sắp xếp theo lần hoạt động gần nhất.
                Dữ liệu gồm mã phiên, các mốc thời gian, thông tin thiết bị rút gọn và cờ `isCurrent` nếu phiên đó
                chính là phiên của người quản trị đang gọi API. Không trả refresh token hoặc địa chỉ IP.

                Yêu cầu Bearer token có quyền `identity.sessions.revoke` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm danh sách `SessionDto`.
                - Không tìm thấy người dùng: 404 Not Found (`Identity.UserNotFound`).
                - Thiếu token hoặc token không hợp lệ: 401 Unauthorized.
                - Không đủ quyền: 403 Forbidden.
                """)
            .Produces<ApiResponse<IReadOnlyList<SessionDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        group.MapDelete("/{sessionId:guid}", RevokeUserSessionAsync)
            .WithSummary("Thu hồi một phiên đăng nhập của người dùng")
            .WithDescription("""
                Thu hồi ngay một phiên đang hoạt động của tài khoản được chỉ định với lý do `AdministratorRevoked`.
                Mọi access token và refresh token thuộc phiên đó sẽ không còn được chấp nhận. Thao tác được ghi Audit Log.
                Nếu người quản trị thu hồi chính phiên hiện tại, refresh cookie trên client cũng bị xóa.

                Yêu cầu Bearer token có quyền `identity.sessions.revoke` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm `revokedCount = 1` và cờ `currentSessionRevoked`.
                - Không tìm thấy người dùng: 404 Not Found (`Identity.UserNotFound`).
                - Không tìm thấy phiên đang hoạt động thuộc người dùng: 404 Not Found (`Identity.SessionNotFound`).
                - Thiếu token hoặc token không hợp lệ: 401 Unauthorized.
                - Không đủ quyền: 403 Forbidden.
                """)
            .Produces<ApiResponse<RevokeSessionsResult>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        group.MapDelete("/", RevokeAllUserSessionsAsync)
            .WithSummary("Thu hồi toàn bộ phiên đăng nhập của người dùng")
            .WithDescription("""
                Thu hồi toàn bộ phiên đăng nhập còn hoạt động của tài khoản được chỉ định với lý do
                `AdministratorRevoked`. Tất cả access token và refresh token liên quan bị vô hiệu hóa ngay;
                thao tác và tổng số phiên đã thu hồi được ghi Audit Log.

                Yêu cầu Bearer token có quyền `identity.sessions.revoke` hoặc người dùng có vai trò `Admin`.
                - Thành công: 200 OK kèm tổng số phiên đã thu hồi; kết quả vẫn thành công khi số lượng bằng 0.
                - Không tìm thấy người dùng: 404 Not Found (`Identity.UserNotFound`).
                - Thiếu token hoặc token không hợp lệ: 401 Unauthorized.
                - Không đủ quyền: 403 Forbidden.
                """)
            .Produces<ApiResponse<RevokeSessionsResult>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetUserSessionsAsync(
        Guid userId,
        GetUserSessionsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(userId, cancellationToken)).ToHttpResult();

    private static async Task<IResult> RevokeUserSessionAsync(
        Guid userId,
        Guid sessionId,
        HttpContext context,
        RefreshCookieService cookies,
        RevokeUserSessionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(userId, sessionId, cancellationToken);
        DeleteCurrentSessionCookie(result, context, cookies);
        return result.ToHttpResult();
    }

    private static async Task<IResult> RevokeAllUserSessionsAsync(
        Guid userId,
        HttpContext context,
        RefreshCookieService cookies,
        RevokeAllUserSessionsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(userId, cancellationToken);
        DeleteCurrentSessionCookie(result, context, cookies);
        return result.ToHttpResult();
    }

    private static void DeleteCurrentSessionCookie(
        UGTM.BuildingBlocks.Domain.Result<RevokeSessionsResult> result,
        HttpContext context,
        RefreshCookieService cookies)
    {
        if (result.IsSuccess && result.Value.CurrentSessionRevoked)
        {
            cookies.Delete(context.Response);
        }
    }
}
