using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.AuditLogs.Dtos;
using UGTM.Modules.Identity.Application.Authentication.Logout;
using UGTM.Modules.Identity.Application.Authentication.GetCurrentUser;
using UGTM.Modules.Identity.Application.Authentication.RefreshSession;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Application.Profiles.GetMyActivity;
using UGTM.Modules.Identity.Application.Profiles.UpdateMyProfile;
using UGTM.Modules.Identity.Application.Sessions;
using UGTM.Modules.Identity.Infrastructure.Authentication;
using UGTM.Modules.Identity.Infrastructure.Authorization;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class SessionEndpoints
{
    public sealed record UpdateMyProfileRequest(string? PhoneNumber);

    public static RouteGroupBuilder MapSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me", GetCurrentUserAsync)
            .WithSummary("Lấy thông tin người dùng đang đăng nhập")
            .WithDescription("""
                Trả thông tin hồ sơ và quyền truy cập hiện tại của người dùng đã đăng nhập.
                Request không có body và không yêu cầu CSRF token.

                Yêu cầu header `Authorization: Bearer <accessToken>` nhận từ endpoint login hoặc refresh.
                Dữ liệu `roles`, `permissions`, `permissionDetails` và `mustChangePassword` được đọc theo trạng thái hiện tại của hệ thống.
                Endpoint này vẫn được phép gọi khi tài khoản đang ở trạng thái bắt buộc đổi mật khẩu (`mustChangePassword = true`).
                - Thành công: 200 OK kèm thông tin hồ sơ người dùng trong `data`.
                - Thiếu token, token sai hoặc hết hạn: 401 Unauthorized.
                - Phiên đã bị thu hồi do logout, đổi/reset mật khẩu, đổi quyền hoặc thay đổi trạng thái tài khoản: 401 Unauthorized,
                  kể cả khi thời hạn JWT chưa kết thúc.
                """)
            .RequireAuthorization()
            .AllowWhenPasswordChangeRequired();
        group.MapPatch("/me", UpdateMyProfileAsync)
            .WithSummary("Cập nhật số điện thoại liên hệ của người dùng đang đăng nhập")
            .WithDescription("""
                Cho phép người dùng tự cập nhật số điện thoại liên hệ (`phoneNumber`).
                Họ tên, email và mã định danh do nhà trường quản lý nên không thể thay đổi qua endpoint này.
                - Thành công: 200 OK kèm `UserProfileDto` mới nhất.
                - Số điện thoại sai định dạng: 400 Bad Request (`Identity.InvalidPhoneNumber`).
                - Thiếu token hoặc phiên không hợp lệ: 401 Unauthorized.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<UserProfileDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
        group.MapGet("/activity", GetMyActivityAsync)
            .WithSummary("Lấy nhật ký hoạt động bảo mật gần đây của người dùng hiện tại")
            .WithDescription("""
                Trả danh sách sự kiện bảo mật gần nhất phát sinh bởi tài khoản đang đăng nhập
                (đăng nhập, đổi mật khẩu, thu hồi phiên, cập nhật liên hệ), sắp xếp mới nhất trước.
                Hỗ trợ lọc theo nhóm hành động (`category`: `auth`, `password`, `session`, `profile`)
                và theo ngày (`date`: `yyyy-MM-dd` theo giờ Việt Nam UTC+7).
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<PagedResult<AuditLogDto>>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
        group.MapGet("/activity/summary", GetMyActivitySummaryAsync)
            .WithSummary("Tổng hợp mật độ hoạt động bảo mật theo ngày (heatmap)")
            .WithDescription("""
                Trả dữ liệu tổng hợp số sự kiện theo từng ngày (UTC+7) trong khoảng `days` ngày gần nhất
                (mặc định 112 ngày = 16 tuần) cùng số cảnh báo đăng nhập/từ chối trong 7 ngày gần nhất.
                """)
            .RequireAuthorization()
            .Produces<ApiResponse<UserActivitySummaryDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
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

    private static async Task<IResult> UpdateMyProfileAsync(
        [FromBody] UpdateMyProfileRequest? request,
        UpdateMyProfileHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new UpdateMyProfileCommand(request?.PhoneNumber),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetMyActivityAsync(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? category = null,
        [FromQuery] string? date = null,
        GetMyActivityHandler handler = null!,
        CancellationToken cancellationToken = default)
        => (await handler.HandleAsync(pageNumber, pageSize, category, date, cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetMyActivitySummaryAsync(
        [FromQuery] int days = 112,
        GetMyActivitySummaryHandler handler = null!,
        CancellationToken cancellationToken = default)
        => (await handler.HandleAsync(days, cancellationToken)).ToHttpResult();

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
