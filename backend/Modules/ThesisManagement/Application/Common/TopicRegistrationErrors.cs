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

    public static readonly Error OnlyAdvisorsAllowed = Error.Forbidden(
        "TopicRegistration.OnlyAdvisorsAllowed",
        "Chỉ giảng viên hướng dẫn hoặc quản trị viên mới có quyền thực hiện thao tác này.");

    public static readonly Error NotTopicAdvisor = Error.Forbidden(
        "TopicRegistration.NotTopicAdvisor",
        "Bạn không phải là giảng viên hướng dẫn phụ trách đề tài này.");

    public static readonly Error TopicPreparationWindowClosed = Error.Conflict(
        "TopicRegistration.TopicPreparationWindowClosed",
        "Giai đoạn chuẩn bị đề tài (TOPIC_PREPARATION) của đợt khóa luận hiện không mở.");

    public static readonly Error InvalidTopicStatusForAdvisorAssign = Error.Conflict(
        "TopicRegistration.InvalidTopicStatusForAdvisorAssign",
        "Chỉ có thể gán hoặc thay đổi nhóm sinh viên khi đề tài ở trạng thái Draft, Submitted, NeedRevision hoặc Approved.");

    public static readonly Error DuplicateStudentInGroup = Error.Validation(
        "TopicRegistration.DuplicateStudentInGroup",
        "Danh sách sinh viên chứa thông tin trùng lặp.");

    public static readonly Error EmptyStudentList = Error.Validation(
        "TopicRegistration.EmptyStudentList",
        "Danh sách sinh viên đăng ký không được để trống.");

    public static readonly Error StudentNotFound = Error.NotFound(
        "TopicRegistration.StudentNotFound",
        "Không tìm thấy sinh viên theo mã hoặc ID được cung cấp, hoặc tài khoản chưa kích hoạt.");

    public static readonly Error RegistrationNotFound = Error.NotFound(
        "TopicRegistration.RegistrationNotFound",
        "Đề tài hiện chưa có nhóm sinh viên nào được gán để hủy.");
}
