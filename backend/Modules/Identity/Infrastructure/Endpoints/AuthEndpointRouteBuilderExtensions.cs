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
            .WithTags("Identity.Auth")
            .AddEndpointFilter<SensitiveResponseHeadersFilter>();

        group.MapGet("/csrf", CsrfEndpoint.Handle)
            .WithSummary("Khởi tạo CSRF token cho các thao tác xác thực dùng cookie")
            .WithDescription("""
                Khởi tạo cặp CSRF token dùng để bảo vệ các endpoint xác thực có sử dụng cookie:
                `login`, `refresh` và `logout`. Endpoint không yêu cầu Bearer token.

                Hệ thống trả request token tại `data.token` và đồng thời thiết lập CSRF cookie HttpOnly.
                Client phải giữ cookie, sau đó gửi giá trị `data.token` qua header `X-CSRF-TOKEN`
                trên request cần bảo vệ. Với trình duyệt sử dụng Fetch API, cần bật `credentials: 'include'`.

                - Thành công: 200 OK kèm `data.token` và CSRF cookie.
                - Request token và cookie phải thuộc cùng một phiên trình duyệt; không được trộn token giữa các client.
                - Khi danh tính Bearer thay đổi hoặc CSRF token không còn hợp lệ, client cần gọi lại endpoint này.
                """)
            .AllowAnonymous();
        group.MapPost("/login", LoginEndpoint.HandleAsync)
            .WithSummary("Đăng nhập bằng email hoặc mã người dùng")
            .WithDescription("""
                Xác thực người dùng bằng `identifier` (email hoặc mã người dùng) và `password`.
                Identifier được loại bỏ khoảng trắng ở hai đầu và so khớp không phân biệt chữ hoa, chữ thường.

                Trước khi gọi, client phải lấy token từ `GET /api/identity/auth/csrf`, giữ CSRF cookie
                và gửi request token qua header `X-CSRF-TOKEN`. Endpoint không yêu cầu Bearer token.

                Khi đăng nhập thành công, hệ thống trả `accessToken`, thời điểm hết hạn và thông tin người dùng
                trong `data`; refresh token chỉ được lưu trong cookie HttpOnly và không xuất hiện trong response body.
                - Thành công: 200 OK kèm thông tin phiên đăng nhập.
                - Thiếu hoặc sai CSRF token: 400 Bad Request.
                - Identifier/mật khẩu sai hoặc tài khoản không ở trạng thái cho phép đăng nhập:
                  401 Unauthorized (`Identity.InvalidCredentials`) để hạn chế dò trạng thái tài khoản.
                - Tài khoản bị khóa tạm thời sau 5 lần đăng nhập sai liên tiếp: 429 Too Many Requests (`Identity.AccountLocked`).
                - Vượt quá 10 request/phút/IP: 429 Too Many Requests (`Identity.RateLimitExceeded`).
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Login);
        group.MapActivationEndpoints();
        group.MapSessionEndpoints();
        group.MapPasswordEndpoints();

        return endpoints;
    }
}
