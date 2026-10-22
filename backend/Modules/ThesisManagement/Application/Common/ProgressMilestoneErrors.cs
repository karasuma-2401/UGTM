using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class ProgressMilestoneErrors
{
    public static readonly Error AccessDenied = Error.Forbidden(
        "ProgressMilestone.AccessDenied",
        "Chỉ GVHD được phân công cho đề tài hoặc Admin mới có quyền quản lý mốc tiến độ.");

    public static readonly Error GroupNotFound = Error.NotFound(
        "ProgressMilestone.GroupNotFound",
        "Không tìm thấy nhóm khóa luận.");

    public static readonly Error GateOneNotApproved = Error.Conflict(
        "ProgressMilestone.GateOneNotApproved",
        "Chỉ có thể tạo mốc tiến độ sau khi nhóm đã được duyệt Đề cương chi tiết tại Cổng 1.");

    public static readonly Error InvalidRequest = Error.Validation(
        "ProgressMilestone.InvalidRequest",
        "Thông tin mốc tiến độ không hợp lệ. Tiêu đề là bắt buộc, dài tối đa 250 ký tự và deadline phải ở tương lai theo UTC.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "ProgressMilestone.ConcurrencyConflict",
        "Mốc tiến độ vừa được một yêu cầu khác cập nhật. Vui lòng tải lại và thử lại.");
}
