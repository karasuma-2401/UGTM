using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;

public static class ReviewerAssignmentErrors
{
    public static readonly Error InvalidRequest = Error.Validation("ReviewerAssignment.InvalidRequest", "Thông tin phân công hoặc Idempotency-Key không hợp lệ.");
    public static readonly Error InvalidCancellationReason = Error.Validation("ReviewerAssignment.InvalidCancellationReason", "Lý do hủy phải có từ 1 đến 1000 ký tự sau khi loại bỏ khoảng trắng đầu/cuối.");
    public static readonly Error NotFound = Error.NotFound("ReviewerAssignment.NotFound", "Không tìm thấy phân công phản biện.");
    public static readonly Error AlreadyCancelled = Error.Conflict("ReviewerAssignment.AlreadyCancelled", "Phân công đã được hủy. Vui lòng tải lại dữ liệu trước khi thao tác.");
    public static readonly Error VersionConflict = Error.Conflict("ReviewerAssignment.VersionConflict", "Phân công đã thay đổi. Vui lòng tải lại phiên bản hiện tại.");
    public static readonly Error EvaluationExists = Error.Conflict("ReviewerAssignment.EvaluationExists", "Không thể hủy phân công đã có phiếu chấm phản biện.");
    public static readonly Error AuthenticationRequired = Error.Unauthorized("ReviewerAssignment.AuthenticationRequired", "Bạn cần đăng nhập để phân công phản biện.");
    public static readonly Error AccessDenied = Error.Forbidden("ReviewerAssignment.AccessDenied", "Bạn không có quyền quản lý phân công phản biện.");
    public static readonly Error GroupNotFound = Error.NotFound("ReviewerAssignment.GroupNotFound", "Không tìm thấy nhóm khóa luận.");
    public static readonly Error ReviewerNotFound = Error.NotFound("ReviewerAssignment.ReviewerNotFound", "Không tìm thấy tài khoản giảng viên phản biện.");
    public static readonly Error ContextInvalid = Error.Conflict("ReviewerAssignment.ContextInvalid", "Thông tin nhóm, đề tài hoặc đợt không nhất quán.");
    public static readonly Error NotEligible = Error.Conflict("ReviewerAssignment.NotEligible", "Nhóm chưa đủ điều kiện hoặc báo cáo không còn khớp bản đã duyệt.");
    public static readonly Error BatchNotOpen = Error.Conflict("ReviewerAssignment.BatchNotOpen", "Đợt khóa luận phải đang mở để tạo phân công.");
    public static readonly Error BindingConflict = Error.Conflict("ReviewerAssignment.BindingConflict", "Bản báo cáo được duyệt đã thay đổi. Vui lòng tải lại.");
    public static readonly Error ReviewerNotEligible = Error.Conflict("ReviewerAssignment.ReviewerNotEligible", "Người phản biện phải là giảng viên có tài khoản đang hoạt động.");
    public static readonly Error AdvisorConflict = Error.Conflict("ReviewerAssignment.AdvisorConflict", "Giảng viên phản biện không được trùng giảng viên hướng dẫn hiện tại.");
    public static readonly Error AlreadyAssigned = Error.Conflict("ReviewerAssignment.AlreadyAssigned", "Nhóm đã có giảng viên phản biện đang được phân công.");
    public static readonly Error IdempotencyKeyReused = Error.Conflict("ReviewerAssignment.IdempotencyKeyReused", "Idempotency-Key đã được dùng cho nội dung khác.");
    public static readonly Error ConcurrencyConflict = Error.Conflict("ReviewerAssignment.ConcurrencyConflict", "Dữ liệu vừa thay đổi. Vui lòng thử lại với cùng Idempotency-Key.");

    internal static Error FromOutcome(ReviewerAssignmentEligibilityOutcome outcome) => outcome switch
    {
        ReviewerAssignmentEligibilityOutcome.GroupNotFound => GroupNotFound,
        ReviewerAssignmentEligibilityOutcome.ContextInvalid => ContextInvalid,
        ReviewerAssignmentEligibilityOutcome.NotEligible => NotEligible,
        ReviewerAssignmentEligibilityOutcome.BatchNotOpen => BatchNotOpen,
        ReviewerAssignmentEligibilityOutcome.BindingConflict => BindingConflict,
        ReviewerAssignmentEligibilityOutcome.ReviewerNotFound => ReviewerNotFound,
        ReviewerAssignmentEligibilityOutcome.ReviewerNotEligible => ReviewerNotEligible,
        ReviewerAssignmentEligibilityOutcome.AdvisorConflict => AdvisorConflict,
        ReviewerAssignmentEligibilityOutcome.AlreadyAssigned => AlreadyAssigned,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unexpected creation outcome.")
    };
}
