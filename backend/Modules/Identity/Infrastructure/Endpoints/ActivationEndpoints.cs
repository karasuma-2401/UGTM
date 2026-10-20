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
                Kiểm tra tính hợp lệ của token trước khi hiển thị màn hình đặt mật khẩu kích hoạt tài khoản.
                Trường `token` chỉ nhận giá trị nằm sau `#token=` trong Magic Link, không nhận toàn bộ URL.
                Endpoint không yêu cầu Bearer token hoặc CSRF token.

                Thao tác này chỉ đọc và không tiêu thụ token; tài khoản vẫn giữ trạng thái `PendingActivation`.
                - Thành công: 200 OK kèm `maskedEmail`, `fullName` và `expiresAt`.
                - Token sai, hết hạn, đã sử dụng hoặc tài khoản không còn chờ kích hoạt: 400 Bad Request (`Identity.InvalidActivationToken`).
                - Vượt quá giới hạn dùng chung 5 request/10 phút/IP của nhóm activation: 429 Too Many Requests (`Identity.RateLimitExceeded`).
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Activation);
        group.MapPost("/activation/complete", CompleteAsync)
            .WithSummary("Kích hoạt tài khoản và đặt mật khẩu")
            .WithDescription("""
                Hoàn tất kích hoạt bằng `token` trong Magic Link và thiết lập `password` đầu tiên cho tài khoản.
                Mật khẩu phải có tối thiểu 12 ký tự, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt.
                Endpoint không yêu cầu Bearer token hoặc CSRF token.

                Khi thành công, hệ thống xác minh email, chuyển tài khoản sang `Active` và tiêu thụ token vĩnh viễn.
                Endpoint không tự tạo phiên; người dùng cần đăng nhập bằng mật khẩu vừa thiết lập.
                - Thành công: 200 OK với `data: null`.
                - Token sai, hết hạn, đã sử dụng hoặc tài khoản không còn chờ kích hoạt: 400 Bad Request (`Identity.InvalidActivationToken`).
                - Mật khẩu không đáp ứng chính sách bảo mật: 400 Bad Request (`Identity.WeakPassword`).
                - Vượt quá giới hạn dùng chung 5 request/10 phút/IP của nhóm activation: 429 Too Many Requests (`Identity.RateLimitExceeded`).
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Activation);
        group.MapPost("/activation/resend", ResendAsync)
            .WithSummary("Gửi lại email kích hoạt")
            .WithDescription("""
                Yêu cầu Magic Link mới bằng `identifier` là email hoặc mã người dùng của tài khoản
                đang ở trạng thái `PendingActivation`. Endpoint không yêu cầu Bearer token hoặc CSRF token.

                Nếu tài khoản hợp lệ, hệ thống vô hiệu hóa token kích hoạt cũ và đưa email mới vào Transactional Outbox
                để gửi bất đồng bộ. Chỉ Magic Link trong email mới nhất còn hiệu lực.
                Để ngăn dò tìm tài khoản, endpoint vẫn trả kết quả thành công khi identifier không tồn tại
                hoặc tài khoản không còn chờ kích hoạt; do đó 200 OK không đồng nghĩa email chắc chắn đã được gửi.
                - Thành công: 200 OK với `data: null`.
                - Vượt quá giới hạn dùng chung 5 request/10 phút/IP của nhóm activation: 429 Too Many Requests (`Identity.RateLimitExceeded`).
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
