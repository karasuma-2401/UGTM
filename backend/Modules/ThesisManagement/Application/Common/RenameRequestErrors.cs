using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class RenameRequestErrors
{
    public static readonly Error OutsideAllowedWindow = Error.Validation(
        "RenameRequest.OutsideAllowedWindow",
        "Chỉ được phép tạo đơn xin đổi tên đề tài trong giai đoạn Tuần 3 đến Tuần 10 của đợt khóa luận.");

    public static readonly Error InvalidRequest = Error.Validation(
        "RenameRequest.InvalidRequest",
        "Thông tin đơn xin đổi tên đề tài không hợp lệ. Vui lòng nhập tên đề tài tiếng Việt mới và lý do điều chỉnh.");

    public static readonly Error TitleUnchanged = Error.Validation(
        "RenameRequest.TitleUnchanged",
        "Tên đề tài mới phải khác với tên đề tài hiện tại.");

    public static readonly Error RejectionReasonRequired = Error.Validation(
        "RenameRequest.RejectionReasonRequired",
        "Bắt buộc nhập lý do từ chối khi không đồng ý đơn đổi tên đề tài.");

    public static readonly Error InvalidDecision = Error.Validation(
        "RenameRequest.InvalidDecision",
        "Quyết định thẩm định chỉ chấp nhận Approve hoặc Reject.");

    public static readonly Error AccessDenied = Error.Forbidden(
        "RenameRequest.AccessDenied",
        "Bạn không có quyền thực hiện thao tác này.");

    public static readonly Error GroupNotFound = Error.NotFound(
        "RenameRequest.GroupNotFound",
        "Không tìm thấy nhóm sinh viên.");

    public static readonly Error TopicNotFound = Error.NotFound(
        "RenameRequest.TopicNotFound",
        "Không tìm thấy đề tài tương ứng với nhóm.");

    public static readonly Error RequestNotFound = Error.NotFound(
        "RenameRequest.RequestNotFound",
        "Không tìm thấy đơn xin đổi tên đề tài.");

    public static readonly Error ActiveRequestAlreadyExists = Error.Conflict(
        "RenameRequest.ActiveRequestAlreadyExists",
        "Nhóm đang có một đơn xin đổi tên đề tài chờ thẩm định.");

    public static readonly Error InvalidGroupState = Error.Conflict(
        "RenameRequest.InvalidGroupState",
        "Trạng thái nhóm hiện tại không cho phép nộp đơn xin đổi tên đề tài.");

    public static readonly Error InvalidReviewState = Error.Conflict(
        "RenameRequest.InvalidReviewState",
        "Đơn xin đổi tên đề tài không ở trạng thái hợp lệ để thực hiện xét duyệt này.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "RenameRequest.ConcurrencyConflict",
        "Dữ liệu đơn xin đổi tên đề tài đã bị sửa đổi bởi thao tác khác. Vui lòng thử lại.");
}
