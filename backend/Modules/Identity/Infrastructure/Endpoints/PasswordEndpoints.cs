using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.Modules.Identity.Application.Authentication.ChangePassword;
using UGTM.Modules.Identity.Application.Recovery;
using UGTM.Modules.Identity.Infrastructure.Authentication;
using UGTM.Modules.Identity.Infrastructure.RateLimiting;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class PasswordEndpoints
{
    public sealed record StartRequest(string Email);
    public sealed record VerifyRequest(string ChallengeId, string Code);
    public sealed record ResetRequest(string ResetGrant, string NewPassword);
    public sealed record ChangeRequest(string CurrentPassword, string NewPassword);

    public static RouteGroupBuilder MapPasswordEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/change-password", ChangeAsync)
            .WithSummary("Đổi mật khẩu của người dùng đang đăng nhập")
            .WithDescription("""
                Thay đổi mật khẩu cho người dùng hiện đang đăng nhập bằng `currentPassword` và `newPassword`.
                Mật khẩu mới phải có tối thiểu 12 ký tự, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt.

                Yêu cầu Bearer access token hợp lệ; endpoint không sử dụng CSRF token.
                Khi thành công, Security Stamp được làm mới, toàn bộ phiên đăng nhập của người dùng bị thu hồi
                và refresh cookie hiện tại bị xóa. Người dùng phải đăng nhập lại bằng mật khẩu mới.
                - Thành công: 200 OK với `data: null`.
                - Mật khẩu hiện tại không đúng: 400 Bad Request (`Identity.CurrentPasswordIncorrect`).
                - Mật khẩu mới không đáp ứng chính sách bảo mật: 400 Bad Request (`Identity.WeakPassword`).
                - Thiếu token, token không hợp lệ hoặc phiên đã bị thu hồi: 401 Unauthorized.
                """)
            .RequireAuthorization();
        group.MapPost("/recovery/start", StartAsync)
            .WithSummary("Gửi OTP khôi phục mật khẩu qua email")
            .WithDescription("""
                Khởi tạo yêu cầu khôi phục mật khẩu cho tài khoản `Active` bằng địa chỉ `email`.
                Endpoint không yêu cầu Bearer token hoặc CSRF token.

                Nếu tài khoản hợp lệ, OTP gồm 6 chữ số được đưa vào Transactional Outbox để gửi email bất đồng bộ
                và mặc định có hiệu lực 10 phút. Client phải lưu `challengeId` để thực hiện bước xác minh.
                Để ngăn dò tìm tài khoản, email rỗng, không tồn tại hoặc không thuộc tài khoản Active vẫn nhận
                `challengeId` giả; challenge này không thể xác minh và không phát sinh email.
                - Thành công: 200 OK kèm `challengeId` và `expiresAt`.
                - Vượt quá giới hạn dùng chung 5 request/10 phút/IP của nhóm recovery: 429 Too Many Requests (`Identity.RateLimitExceeded`).
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Recovery);
        group.MapPost("/recovery/verify", VerifyAsync)
            .WithSummary("Xác minh OTP và lấy quyền đặt lại mật khẩu")
            .WithDescription("""
                Xác minh OTP bằng `challengeId` nhận từ bước `recovery/start` và `code` trong email.
                `code` phải là chuỗi 6 chữ số và cần giữ nguyên số 0 ở đầu nếu có.
                Endpoint không yêu cầu Bearer token hoặc CSRF token.

                Khi thành công, OTP được tiêu thụ và hệ thống cấp `resetGrant` dùng một lần, mặc định có hiệu lực 10 phút.
                - Thành công: 200 OK kèm `resetGrant` và `expiresAt`.
                - OTP sai, hết hạn, đã sử dụng hoặc vượt quá 5 lần thử: 400 Bad Request (`Identity.InvalidOtp`).
                - Vượt quá giới hạn dùng chung 5 request/10 phút/IP của nhóm recovery: 429 Too Many Requests (`Identity.RateLimitExceeded`).
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Recovery);
        group.MapPost("/recovery/reset", ResetAsync)
            .WithSummary("Đặt lại mật khẩu bằng reset grant")
            .WithDescription("""
                Đặt mật khẩu mới bằng `resetGrant` nhận từ bước `recovery/verify` và trường `newPassword`.
                Mật khẩu mới phải có tối thiểu 12 ký tự, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt.
                Endpoint không yêu cầu Bearer token hoặc CSRF token.

                Khi thành công, reset grant được tiêu thụ, Security Stamp được làm mới, toàn bộ phiên đăng nhập
                của tài khoản bị thu hồi và refresh cookie hiện tại bị xóa. Người dùng phải đăng nhập lại.
                - Thành công: 200 OK với `data: null`.
                - Reset grant sai, hết hạn hoặc đã sử dụng: 400 Bad Request (`Identity.InvalidResetGrant`).
                - Mật khẩu mới không đáp ứng chính sách bảo mật: 400 Bad Request (`Identity.WeakPassword`).
                - Vượt quá giới hạn dùng chung 5 request/10 phút/IP của nhóm recovery: 429 Too Many Requests (`Identity.RateLimitExceeded`).
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Recovery);
        return group;
    }

    private static async Task<IResult> ChangeAsync(
        ChangeRequest request,
        HttpContext context,
        ChangePasswordHandler handler,
        RefreshCookieService cookies,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ChangePasswordCommand(request.CurrentPassword, request.NewPassword),
            cancellationToken);

        if (result.IsSuccess)
        {
            cookies.Delete(context.Response);
        }

        return result.ToHttpResult();
    }

    private static async Task<IResult> StartAsync(
        StartRequest request,
        StartPasswordRecoveryHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new StartPasswordRecoveryCommand(request.Email),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> VerifyAsync(
        VerifyRequest request,
        VerifyRecoveryOtpHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new VerifyRecoveryOtpCommand(request.ChallengeId, request.Code),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> ResetAsync(
        ResetRequest request,
        HttpContext context,
        ResetPasswordHandler handler,
        RefreshCookieService cookies,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ResetPasswordCommand(request.ResetGrant, request.NewPassword),
            cancellationToken);

        if (result.IsSuccess)
        {
            cookies.Delete(context.Response);
        }

        return result.ToHttpResult();
    }
}
