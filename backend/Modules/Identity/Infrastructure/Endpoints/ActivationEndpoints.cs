using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.Modules.Identity.Application.Activation;
using UGTM.Modules.Identity.Infrastructure.RateLimiting;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class ActivationEndpoints
{
    public sealed record ValidateRequest(string Token);
    public sealed record CompleteRequest(string Token, string Password);
    public sealed record ResendRequest(string Identifier);

    public static RouteGroupBuilder MapActivationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/activation/validate", ValidateAsync)
            .WithSummary("Kiểm tra token kích hoạt tài khoản")
            .WithDescription("""
                Gửi `token` lấy từ phần `#token=` trong link email kích hoạt; chỉ gửi giá trị token, không gửi cả URL.
                Không cần Bearer hoặc CSRF token.

                Thành công trả `maskedEmail`, `fullName`, `expiresAt`; bước này chưa kích hoạt tài khoản và chưa tiêu thụ token.
                Token sai, hết hạn, đã dùng hoặc tài khoản không còn chờ kích hoạt: 400 `Identity.InvalidActivationToken`.
                Các endpoint activation dùng chung giới hạn 5 request/10 phút/IP.
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Activation);
        group.MapPost("/activation/complete", CompleteAsync)
            .WithSummary("Kích hoạt tài khoản và đặt mật khẩu")
            .WithDescription("""
                Gửi `token` từ email kích hoạt và `password` muốn đặt cho tài khoản.
                Mật khẩu cần ít nhất 12 ký tự, có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.
                Không cần Bearer hoặc CSRF token.

                Thành công trả 200, xác minh email và chuyển tài khoản sang Active. Token chỉ dùng một lần.
                Sau đó gọi login để đăng nhập; endpoint này không tự tạo phiên.
                Token không hợp lệ: 400 `Identity.InvalidActivationToken`; mật khẩu yếu: 400 `Identity.WeakPassword`.
                Các endpoint activation dùng chung giới hạn 5 request/10 phút/IP.
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Activation);
        group.MapPost("/activation/resend", ResendAsync)
            .WithSummary("Gửi lại email kích hoạt")
            .WithDescription("""
                Gửi `identifier` là email hoặc mã người dùng của tài khoản đang chờ kích hoạt.
                Không cần Bearer hoặc CSRF token.

                Token kích hoạt cũ bị vô hiệu và email chứa token mới được đưa vào hàng đợi gửi.
                Chỉ dùng link trong email mới nhất. Email được gửi bất đồng bộ nên có thể chưa đến ngay khi API trả về.
                Endpoint vẫn trả 200 nếu tài khoản không tồn tại hoặc không chờ kích hoạt để tránh lộ thông tin tài khoản;
                HTTP 200 không bảo đảm đã gửi email.
                Các endpoint activation dùng chung giới hạn 5 request/10 phút/IP.
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Activation);
        return group;
    }

    private static async Task<IResult> ValidateAsync(
        ValidateRequest request,
        ValidateActivationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new ValidateActivationCommand(request.Token),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> CompleteAsync(
        CompleteRequest request,
        CompleteActivationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new CompleteActivationCommand(request.Token, request.Password),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> ResendAsync(
        ResendRequest request,
        ResendActivationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new ResendActivationCommand(request.Identifier),
            cancellationToken)).ToHttpResult();
}
