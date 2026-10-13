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
using UGTM.Modules.Identity.Infrastructure.Authentication;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me", GetCurrentUserAsync)
            .WithSummary("Lấy thông tin người dùng đang đăng nhập")
            .WithDescription("""
                Gửi header `Authorization: Bearer <accessToken>` lấy từ login hoặc refresh. Không có body, không cần CSRF.

                Thành công trả profile trong `data`, gồm id, email, fullName, userCode, roles và permissions hiện tại từ database.
                JWT thiếu, sai, hết hạn hoặc phiên đã bị thu hồi: 401, body có thể rỗng.
                JWT còn hạn vẫn bị từ chối sau logout, đổi mật khẩu hoặc reset mật khẩu làm phiên mất hiệu lực.
                """)
            .RequireAuthorization();
        group.MapPost("/refresh", RefreshAsync)
            .WithSummary("Làm mới access token từ refresh cookie")
            .WithDescription("""
                Không có body và không cần Bearer token. Gửi refresh cookie của phiên đã đăng nhập,
                cookie CSRF và header `X-CSRF-TOKEN` lấy từ endpoint csrf.

                Thành công trả access token, thời hạn và user giống login, đồng thời thay refresh cookie bằng token mới.
                Refresh token cũ chỉ dùng một lần; dùng lại token đã tiêu thụ sẽ thu hồi phiên.
                Cookie thiếu hoặc phiên không hợp lệ: 401 `Identity.InvalidSession`; thiếu hoặc sai CSRF: 400.
                """)
            .AllowAnonymous();
        group.MapPost("/logout", LogoutAsync)
            .WithSummary("Đăng xuất khỏi phiên hiện tại")
            .WithDescription("""
                Không có body và không cần Bearer token. Gửi refresh cookie, cookie CSRF và header `X-CSRF-TOKEN`.

                Backend thu hồi phiên gắn với refresh cookie và xóa refresh cookie; JWT của phiên đó mất hiệu lực.
                Thành công trả 200 với `data: null`. Gọi lại hoặc không có refresh cookie vẫn trả 200.
                CSRF vẫn bắt buộc trong mọi trường hợp; thiếu hoặc sai CSRF: 400.
                """)
            .AllowAnonymous();
        return group;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        GetCurrentUserHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(cancellationToken)).ToHttpResult();

    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        RefreshCookieService cookies,
        RefreshSessionHandler handler,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
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
        await antiforgery.ValidateRequestAsync(context);
        var result = await handler.HandleAsync(
            new LogoutCommand(cookies.Read(context.Request)),
            cancellationToken);
        cookies.Delete(context.Response);
        return result.ToHttpResult();
    }
}
