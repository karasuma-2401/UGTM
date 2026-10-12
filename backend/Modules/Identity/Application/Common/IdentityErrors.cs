using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.Identity.Application.Common;

public static class IdentityErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Identity.InvalidCredentials",
        "Email, mã sinh viên hoặc mật khẩu không đúng.");

    public static readonly Error AccountPendingActivation = Error.Forbidden(
        "Identity.AccountPendingActivation",
        "Tài khoản chưa được kích hoạt.");

    public static readonly Error AccountSuspended = Error.Forbidden(
        "Identity.AccountSuspended",
        "Tài khoản đang bị tạm ngưng.");

    public static readonly Error AccountDisabled = Error.Forbidden(
        "Identity.AccountDisabled",
        "Tài khoản đã bị vô hiệu hóa.");

    public static readonly Error AccountLocked = Error.TooManyRequests(
        "Identity.AccountLocked",
        "Tài khoản đang bị khóa tạm thời. Vui lòng thử lại sau.");

    public static readonly Error DuplicateAccount = Error.Conflict(
        "Identity.DuplicateAccount",
        "Email hoặc mã sinh viên đã tồn tại.");

    public static readonly Error UserNotFound = Error.NotFound(
        "Identity.UserNotFound",
        "Không tìm thấy tài khoản.");

    public static readonly Error InvalidActivationToken = Error.Validation(
        "Identity.InvalidActivationToken",
        "Liên kết kích hoạt không hợp lệ hoặc đã hết hạn.");

    public static readonly Error InvalidOtp = Error.Validation(
        "Identity.InvalidOtp",
        "Mã xác thực không hợp lệ hoặc đã hết hạn.");

    public static readonly Error InvalidResetGrant = Error.Validation(
        "Identity.InvalidResetGrant",
        "Quyền đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");

    public static readonly Error WeakPassword = Error.Validation(
        "Identity.WeakPassword",
        "Mật khẩu không đáp ứng chính sách bảo mật.");

    public static readonly Error CurrentPasswordIncorrect = Error.Validation(
        "Identity.CurrentPasswordIncorrect",
        "Mật khẩu hiện tại không đúng.");

    public static readonly Error InvalidSession = Error.Unauthorized(
        "Identity.InvalidSession",
        "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
}