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
                Gửi Bearer access token hợp lệ cùng `currentPassword` và `newPassword`. Không cần CSRF.
                Mật khẩu mới cần ít nhất 12 ký tự, có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.

                Thành công trả 200 với `data: null`, thu hồi mọi phiên của người dùng và xóa refresh cookie hiện tại.
                Phải đăng nhập lại bằng mật khẩu mới.
                Mật khẩu hiện tại sai: 400 `Identity.CurrentPasswordIncorrect`; mật khẩu mới yếu: 400 `Identity.WeakPassword`.
                Bearer token thiếu hoặc không hợp lệ: 401.
                """)
            .RequireAuthorization();
        group.MapPost("/recovery/start", StartAsync)
            .WithSummary("Gửi OTP khôi phục mật khẩu qua email")
            .WithDescription("""
                Gửi `email` của tài khoản Active. Không cần Bearer hoặc CSRF token.

                Thành công trả `data.challengeId` và `data.expiresAt`. Lưu challengeId để gọi recovery/verify;
                OTP 6 chữ số được gửi bất đồng bộ qua email và mặc định sống 10 phút.
                Email không tồn tại, rỗng hoặc tài khoản không Active vẫn nhận response 200 có challengeId giả;
                trường hợp đó không gửi email và không thể verify thành công.
                Các endpoint recovery dùng chung giới hạn 5 request/10 phút/IP.
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Recovery);
        group.MapPost("/recovery/verify", VerifyAsync)
            .WithSummary("Xác minh OTP và lấy quyền đặt lại mật khẩu")
            .WithDescription("""
                Gửi `challengeId` từ recovery/start và `code` là OTP trong email.
                Code là chuỗi 6 chữ số; giữ số 0 ở đầu nếu có. Không cần Bearer hoặc CSRF token.

                Thành công tiêu thụ OTP và trả `data.resetGrant`, `data.expiresAt` để gọi recovery/reset.
                Reset grant chỉ dùng một lần, mặc định sống 10 phút.
                OTP sai, hết hạn, đã dùng hoặc vượt 5 lần thử: 400 `Identity.InvalidOtp`.
                Các endpoint recovery dùng chung giới hạn 5 request/10 phút/IP.
                """)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Recovery);
        group.MapPost("/recovery/reset", ResetAsync)
            .WithSummary("Đặt lại mật khẩu bằng reset grant")
            .WithDescription("""
                Gửi `resetGrant` từ recovery/verify và `newPassword`.
                Mật khẩu mới cần ít nhất 12 ký tự, có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.
                Không cần Bearer hoặc CSRF token.

                Thành công trả 200 với `data: null`, tiêu thụ reset grant, thu hồi mọi phiên và xóa refresh cookie hiện tại.
                Phải đăng nhập lại bằng mật khẩu mới.
                Grant sai, hết hạn hoặc đã dùng: 400 `Identity.InvalidResetGrant`; mật khẩu yếu: 400 `Identity.WeakPassword`.
                Các endpoint recovery dùng chung giới hạn 5 request/10 phút/IP.
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
