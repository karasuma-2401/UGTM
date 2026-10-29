using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;

public static class AdvisorEvaluationErrors
{
    public static readonly Error InvalidRequest = Error.Validation(
        "AdvisorEvaluation.InvalidRequest", "Thông tin phiếu chấm của GVHD không hợp lệ.");
    public static readonly Error InvalidScore = Error.Validation(
        "AdvisorEvaluation.InvalidScore", "Điểm GVHD phải nằm trong khoảng từ 0 đến 10.");
    public static readonly Error InvalidComment = Error.Validation(
        "AdvisorEvaluation.InvalidComment", "Nhận xét GVHD phải có từ 1 đến 2000 ký tự.");
    public static readonly Error AuthenticationRequired = Error.Unauthorized(
        "AdvisorEvaluation.AuthenticationRequired", "Bạn cần đăng nhập để chấm báo cáo cuối kỳ.");
    public static readonly Error AccessDenied = Error.Forbidden(
        "AdvisorEvaluation.AccessDenied", "Chỉ GVHD hiện được phân công hoặc Admin được chấm báo cáo cuối kỳ.");
    public static readonly Error FinalReportNotFound = Error.NotFound(
        "FinalReport.ReportNotFound", "Không tìm thấy báo cáo cuối kỳ.");
    public static readonly Error GroupNotFound = Error.NotFound(
        "FinalReport.GroupNotFound", "Không tìm thấy nhóm khóa luận.");
    public static readonly Error FileNotFound = Error.NotFound(
        "FinalReport.FileNotFound", "Không tìm thấy tệp báo cáo cuối kỳ đã được khóa để chấm.");
    public static readonly Error InvalidEvaluationState = Error.Conflict(
        "FinalReport.InvalidEvaluationState", "Báo cáo phải là InitialFinal đang ở trạng thái UnderEvaluation.");
    public static readonly Error InvalidGroupState = Error.Conflict(
        "FinalReport.InvalidGroupState", "Nhóm phải ở trạng thái đã nộp báo cáo cuối kỳ để GVHD chấm.");
    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "FinalReport.ConcurrencyConflict", "Báo cáo hoặc nhóm vừa được thay đổi. Vui lòng tải lại và thử lại.");
    public static readonly Error VersionConflict = Error.Conflict(
        "AdvisorEvaluation.VersionConflict", "Phiếu chấm đã được thay đổi. Vui lòng tải lại trước khi cập nhật.");
    public static readonly Error AlreadyExists = Error.Conflict(
        "AdvisorEvaluation.AlreadyExists", "Phiếu chấm của GVHD đã tồn tại; cần cung cấp phiên bản hiện tại để cập nhật.");
    public static readonly Error NotFound = Error.NotFound(
        "AdvisorEvaluation.NotFound", "Không tìm thấy phiếu chấm để cập nhật.");
    public static readonly Error Locked = Error.Conflict(
        "AdvisorEvaluation.Locked", "Phiếu chấm đã được duyệt và không thể chỉnh sửa.");
    public static readonly Error IdempotencyKeyReused = Error.Conflict(
        "AdvisorEvaluation.IdempotencyKeyReused", "Idempotency-Key này đã được dùng cho nội dung yêu cầu khác.");
    public static readonly Error LegacyRecordNeedsResolution = Error.Conflict(
        "AdvisorEvaluation.LegacyRecordNeedsResolution", "Phiếu chấm cũ chưa có liên kết báo cáo khóa; cần được xử lý riêng trước khi chấm.");
}
