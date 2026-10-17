namespace UGTM.Modules.Identity.Application.Common;

public static class IdentityErrors
{
    public static readonly Error StudentRoleConflict = Error.Validation(
        "Identity.StudentRoleConflict",
        "Vai trò Sinh viên không được kết hợp với vai trò khác.");

    public static readonly Error CannotBecomeStudent = Error.Validation(
        "Identity.CannotBecomeStudent",
        "Tài khoản đã có vai trò khác không được chuyển thành Sinh viên.");

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

    public static readonly Error InvalidInput = Error.Validation(
        "Identity.InvalidInput",
        "Dữ liệu đầu vào không hợp lệ.");

    public static readonly Error InvalidRole = Error.Validation(
        "Identity.InvalidRole",
        "Vai trò người dùng không hợp lệ hoặc không được để trống.");

    public static readonly Error InvalidStatus = Error.Validation(
        "Identity.InvalidStatus",
        "Trạng thái tài khoản không hợp lệ.");

    public static readonly Error InvalidStatusTransition = Error.Validation(
        "Identity.InvalidStatusTransition",
        "Không thể chuyển đổi trạng thái tài khoản theo yêu cầu.");

    public static readonly Error InvalidPhoneNumber = Error.Validation(
        "Identity.InvalidPhoneNumber",
        "Số điện thoại không hợp lệ (yêu cầu định dạng 10 chữ số, ví dụ: 0901234567 hoặc +84901234567).");

    public static readonly Error RoleNotFound = Error.NotFound(
        "Identity.RoleNotFound",
        "Không tìm thấy vai trò người dùng.");

    public static readonly Error InvalidPermission = Error.Validation(
        "Identity.InvalidPermission",
        "Một hoặc nhiều quyền được chọn không tồn tại hoặc không hợp lệ.");

    public static readonly Error CannotRevokeAllAdminPermissions = Error.Validation(
        "Identity.CannotRevokeAllAdminPermissions",
        "Không thể gỡ bỏ toàn bộ quyền của vai trò Quản trị viên (Admin).");

    public static readonly Error InvalidAuditLogFilter = Error.Validation(
        "Identity.InvalidAuditLogFilter",
        "Bộ lọc nhật ký hệ thống không hợp lệ.");
}
