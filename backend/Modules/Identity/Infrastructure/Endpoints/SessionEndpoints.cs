using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.Authentication.Logout;
using UGTM.Modules.Identity.Application.Authentication.GetCurrentUser;
using UGTM.Modules.Identity.Application.Authentication.RefreshSession;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Sessions;
using UGTM.Modules.Identity.Infrastructure.Authentication;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me", GetCurrentUserAsync)
            .WithSummary("Lấy thông tin người dùng đang đăng nhập")
            .WithDescription("""
                Trả thông tin hồ sơ và quyền truy cập hiện tại của người dùng đã đăng nhập.
                Request không có body và không yêu cầu CSRF token.

                Yêu cầu header `Authorization: Bearer <accessToken>` nhận từ endpoint login hoặc refresh.
                Dữ liệu `roles` và `permissions` được đọc theo trạng thái hiện tại của hệ thống.
                - Thành công: 200 OK kèm `id`, `email`, `fullName`, `userCode`, `roles` và `permissions` trong `data`.
                - Thiếu token, token sai hoặc hết hạn: 401 Unauthorized.
                - Phiên đã bị thu hồi do logout, đổi/reset mật khẩu, đổi quyền hoặc thay đổi trạng thái tài khoản: 401 Unauthorized,
                  kể cả khi thời hạn JWT chưa kết thúc.
                """)
            .RequireAuthorization();
        group.MapGet("/sessions", GetMySessionsAsync)
            .WithSummary("Liệt kê các phiên đăng nhập đang hoạt động")
            .WithDescription("""
                Trả danh sách phiên đăng nhập còn hiệu lực của người dùng hiện tại, sắp xếp theo lần hoạt động gần nhất.
                Mỗi phần tử gồm mã phiên, thời điểm đăng nhập, lần hoạt động gần nhất, thời hạn idle/absolute,
                thông tin thiết bị rút gọn và cờ `isCurrent` để nhận biết phiên đang gọi API.

                Request không có body và không yêu cầu CSRF token.
                Yêu cầu header `Authorization: Bearer <accessToken>`.
                - Thành công: 200 OK kèm danh sách `SessionDto`; danh sách có thể rỗng nếu không còn phiên hợp lệ.
                - Thiếu token, token sai/hết hạn hoặc phiên hiện tại đã bị thu hồi: 401 Unauthorized.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<IReadOnlyList<SessionDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
        group.MapDelete("/sessions/{sessionId:guid}", RevokeMySessionAsync)
            .WithSummary("Thu hồi một phiên đăng nhập của chính mình")
            .WithDescription("""
                Thu hồi ngay một phiên đang hoạt động thuộc người dùng hiện tại. Access token và refresh token
                gắn với phiên bị thu hồi sẽ không còn được chấp nhận. Nếu thu hồi chính phiên đang gọi API,
                hệ thống đồng thời xóa refresh cookie trên client.

                Request không có body, không yêu cầu CSRF token và không thể thu hồi phiên của người dùng khác.
                Yêu cầu header `Authorization: Bearer <accessToken>`.
                - Thành công: 200 OK kèm `revokedCount = 1` và cờ `currentSessionRevoked`.
                - Không tìm thấy phiên đang hoạt động thuộc người dùng hiện tại: 404 Not Found (`Identity.SessionNotFound`).
                - Thiếu token, token sai/hết hạn hoặc phiên hiện tại đã bị thu hồi: 401 Unauthorized.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<RevokeSessionsResult>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);
        group.MapDelete("/sessions", RevokeAllMySessionsAsync)
            .WithSummary("Đăng xuất khỏi tất cả thiết bị")
            .WithDescription("""
                Thu hồi toàn bộ phiên đăng nhập còn hoạt động của người dùng hiện tại, bao gồm phiên đang gọi API.
                Sau khi hoàn tất, mọi access token và refresh token của các phiên này đều bị vô hiệu hóa,
                đồng thời refresh cookie trên client hiện tại bị xóa.

                Request không có body và không yêu cầu CSRF token.
                Yêu cầu header `Authorization: Bearer <accessToken>`.
                - Thành công: 200 OK kèm số phiên đã thu hồi và `currentSessionRevoked = true` khi có phiên bị thu hồi.
                - Thiếu token, token sai/hết hạn hoặc phiên hiện tại đã bị thu hồi: 401 Unauthorized.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<RevokeSessionsResult>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
        group.MapPost("/refresh", RefreshAsync)
            .WithSummary("Làm mới access token từ refresh cookie")
            .WithDescription("""
                Cấp access token mới từ refresh cookie của phiên đăng nhập. Request không có body và không yêu cầu Bearer token.
                Client phải gửi refresh cookie, CSRF cookie và header `X-CSRF-TOKEN` tương ứng.

                Hệ thống thực hiện refresh token rotation: token cũ bị tiêu thụ và refresh cookie được thay bằng token mới.
                Việc tái sử dụng refresh token đã tiêu thụ được xem là dấu hiệu replay và làm phiên bị thu hồi.
                - Thành công: 200 OK kèm `accessToken`, `accessTokenExpiresAt`, `user` và refresh cookie mới.
                - Thiếu hoặc sai CSRF token: 400 Bad Request.
                - Thiếu refresh cookie, token sai/hết hạn hoặc phiên đã bị thu hồi: 401 Unauthorized (`Identity.InvalidSession`).
                """)
            .AllowAnonymous();
        group.MapPost("/logout", LogoutAsync)
            .WithSummary("Đăng xuất khỏi phiên hiện tại")
            .WithDescription("""
                Thu hồi phiên gắn với refresh cookie hiện tại và xóa cookie khỏi client.
                Request không có body, không yêu cầu Bearer token nhưng bắt buộc gửi CSRF cookie
                cùng header `X-CSRF-TOKEN` tương ứng.

                Sau khi đăng xuất, access token thuộc phiên này không còn được chấp nhận dù JWT chưa hết hạn.
                Endpoint có tính idempotent: gọi lại hoặc không có refresh cookie vẫn được xem là thành công.
                - Thành công: 200 OK với `data: null` và refresh cookie bị xóa.
                - Thiếu hoặc sai CSRF token: 400 Bad Request.
                """)
            .AllowAnonymous();
        return group;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        GetCurrentUserHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetMySessionsAsync(
        GetMySessionsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(cancellationToken)).ToHttpResult();

    private static async Task<IResult> RevokeMySessionAsync(
        Guid sessionId,
        HttpContext context,
        RefreshCookieService cookies,
        RevokeMySessionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(sessionId, cancellationToken);
        if (result.IsSuccess && result.Value.CurrentSessionRevoked)
        {
            cookies.Delete(context.Response);
        }

        return result.ToHttpResult();
    }

    private static async Task<IResult> RevokeAllMySessionsAsync(
        HttpContext context,
        RefreshCookieService cookies,
        RevokeAllMySessionsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);
        if (result.IsSuccess && result.Value.CurrentSessionRevoked)
        {
            cookies.Delete(context.Response);
        }

        return result.ToHttpResult();
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        RefreshCookieService cookies,
        RefreshSessionHandler handler,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestSafelyAsync(context);
        var refreshToken = cookies.Read(context.Request);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result.Failure<object>(IdentityErrors.InvalidSession).ToHttpResult();
        }

        var result = await handler.HandleAsync(
            new RefreshSessionCommand(refreshToken),
            cancellationToken);
        if (result.IsFailure)
        {
            cookies.Delete(context.Response);
            return result.ToHttpResult();
        }

        cookies.Write(
            context.Response,
            result.Value.Tokens.RefreshToken,
            result.Value.Tokens.RefreshTokenExpiresAt);

        return Results.Ok(ApiResponse<object>.Ok(new
        {
            accessToken = result.Value.Tokens.AccessToken,
            accessTokenExpiresAt = result.Value.Tokens.AccessTokenExpiresAt,
            user = result.Value.User
        }));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        RefreshCookieService cookies,
        LogoutHandler handler,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestSafelyAsync(context);
        var result = await handler.HandleAsync(
            new LogoutCommand(cookies.Read(context.Request)),
            cancellationToken);
        cookies.Delete(context.Response);
        return result.ToHttpResult();
    }
}
