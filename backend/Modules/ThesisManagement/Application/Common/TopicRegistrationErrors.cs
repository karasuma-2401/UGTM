using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class TopicRegistrationErrors
{
    public static readonly Error TopicNotFound = Error.NotFound(
        "TopicRegistration.TopicNotFound",
        "Không tìm thấy đề tài khóa luận.");

    public static readonly Error TopicAlreadyTaken = Error.Conflict(
        "TopicRegistration.TopicAlreadyTaken",
        "Đề tài đã đủ chỉ tiêu sinh viên đăng ký.");

    public static readonly Error TopicNotPublished = Error.Conflict(
        "TopicRegistration.TopicNotPublished",
        "Đề tài chưa được công bố hoặc không mở đăng ký.");

    public static readonly Error RegistrationWindowClosed = Error.Conflict(
        "TopicRegistration.RegistrationWindowClosed",
        "Cổng đăng ký đề tài của đợt khóa luận hiện đang đóng.");

    public static readonly Error StudentAlreadyRegisteredInBatch = Error.Conflict(
        "TopicRegistration.StudentAlreadyRegisteredInBatch",
        "Sinh viên hoặc thành viên cùng nhóm đã đăng ký đề tài trong đợt khóa luận này.");

    public static readonly Error StudentRegistrationBlocked = Error.Forbidden(
        "TopicRegistration.StudentRegistrationBlocked",
        "Tài khoản của bạn đã bị khóa quyền đăng ký đề tài do vi phạm quá hạn xác nhận 3 lần.");

    public static readonly Error PartnerRegistrationBlocked = Error.Forbidden(
        "TopicRegistration.PartnerRegistrationBlocked",
        "Thành viên cùng nhóm đã bị khóa quyền đăng ký đề tài do vi phạm quá hạn xác nhận 3 lần.");

    public static readonly Error GroupSizeExceedsTopicQuota = Error.Validation(
        "TopicRegistration.GroupSizeExceedsTopicQuota",
        "Số lượng thành viên đăng ký vượt quá chỉ tiêu tiếp nhận của đề tài.");

    public static readonly Error PartnerNotFound = Error.NotFound(
        "TopicRegistration.PartnerNotFound",
        "Không tìm thấy sinh viên cùng nhóm được chỉ định hoặc tài khoản không hoạt động.");

    public static readonly Error CannotInviteSelf = Error.Validation(
        "TopicRegistration.CannotInviteSelf",
        "Không thể chỉ định chính mình làm thành viên cùng nhóm.");

    public static readonly Error InvalidConfirmationToken = Error.Validation(
        "TopicRegistration.InvalidConfirmationToken",
        "Mã xác nhận tham gia đề tài không hợp lệ.");

    public static readonly Error ConfirmationExpired = Error.Conflict(
        "TopicRegistration.ConfirmationExpired",
        "Yêu cầu xác nhận tham gia đề tài đã hết hạn.");

    public static readonly Error AlreadyConfirmed = Error.Conflict(
        "TopicRegistration.AlreadyConfirmed",
        "Thành viên đã xác nhận tham gia đề tài từ trước.");

    public static readonly Error NotPendingConfirmation = Error.Conflict(
        "TopicRegistration.NotPendingConfirmation",
        "Thành viên không ở trạng thái chờ xác nhận.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "TopicRegistration.ConcurrencyConflict",
        "Đã xảy ra xung đột khi đăng ký đề tài. Vui lòng thử lại.");
}
