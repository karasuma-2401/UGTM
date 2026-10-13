using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.Modules.Identity.Infrastructure.RateLimiting;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class AuthEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapIdentityAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity/auth")
            .WithTags("Identity.Auth");

        group.MapGet("/csrf", CsrfEndpoint.Handle)
            .WithSummary("Lấy CSRF token")
            .WithDescription("""
                Lấy token trước khi gọi đăng nhập, làm mới phiên hoặc đăng xuất. Không cần Bearer token.

                Lưu `data.token` trong response và gửi qua header `X-CSRF-TOKEN` cho các request tiếp theo.
                Backend đồng thời đặt cookie CSRF HttpOnly; phải giữ và gửi cookie bằng cùng trình duyệt
                hoặc công cụ đã lấy token. Với fetch, dùng `credentials: 'include'`.
                Nếu thay đổi danh tính Bearer hoặc gặp lỗi CSRF, lấy lại token với cùng trạng thái xác thực của request tiếp theo.
                """)
            .AllowAnonymous();
        group.MapPost("/login", LoginEndpoint.HandleAsync)
            .WithSummary("Đăng nhập bằng email hoặc mã người dùng")
            .WithDescription("""
                Gửi `identifier` là email hoặc mã người dùng và `password` là mật khẩu hiện tại.
                Identifier được trim và so khớp không phân biệt hoa thường.

                **Trước khi gọi:** lấy token từ `GET /api/identity/auth/csrf`, gửi header `X-CSRF-TOKEN`
                và cookie CSRF tương ứng. Không cần Bearer token.

                Thành công trả `data.accessToken`, `data.accessTokenExpiresAt`, `data.user` và đặt refresh cookie HttpOnly.
                Dùng access token làm Bearer cho API có bảo vệ; refresh token không được trả trong JSON.

                Sai thông tin đăng nhập: 401. Tài khoản chưa kích hoạt, tạm ngưng hoặc vô hiệu hóa: 403.
                Sai mật khẩu 5 lần liên tiếp sẽ khóa tài khoản 15 phút (429). Giới hạn 10 request/phút/IP.
                Thiếu hoặc sai CSRF: 400.
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Login);
        group.MapActivationEndpoints();
        group.MapSessionEndpoints();
        group.MapPasswordEndpoints();

        return endpoints;
    }
}
