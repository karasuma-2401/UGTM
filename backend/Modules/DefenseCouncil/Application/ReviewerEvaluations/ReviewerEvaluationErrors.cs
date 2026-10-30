using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public static class ReviewerEvaluationErrors
{
    public static readonly Error InvalidRequest = Error.Validation("ReviewerEvaluation.InvalidRequest", "Request, GUID, phiên bản, action, enum hoặc Idempotency-Key không hợp lệ.");
    public static readonly Error InvalidScore = Error.Validation("ReviewerEvaluation.InvalidScore", "Điểm phản biện phải từ 0 đến 10; nháp được để trống.");
    public static readonly Error InvalidComment = Error.Validation("ReviewerEvaluation.InvalidComment", "Góp ý phải là text hợp lệ, tối đa 2.000 Unicode rune.");
    public static readonly Error IncompleteEvaluation = Error.Validation("ReviewerEvaluation.IncompleteEvaluation", "Chưa đủ điểm, góp ý và kết luận để chốt phiếu.");
    public static readonly Error AuthenticationRequired = Error.Unauthorized("ReviewerEvaluation.AuthenticationRequired", "Bạn cần đăng nhập để xem hoặc chấm phản biện.");
    public static readonly Error AccessDenied = Error.Forbidden("ReviewerEvaluation.AccessDenied", "Bạn không có quyền thực hiện thao tác với phiếu này.");
    public static readonly Error AssignmentNotFound = Error.NotFound("ReviewerEvaluation.AssignmentNotFound", "Không tìm thấy phân công phản biện.");
    public static readonly Error NotFound = Error.NotFound("ReviewerEvaluation.NotFound", "Chưa có phiếu phản biện của phân công này.");
    public static readonly Error LegacyRecordNeedsResolution = Error.Conflict("ReviewerEvaluation.LegacyRecordNeedsResolution", "Dữ liệu cũ cần được xử lý theo quy trình riêng.");
    public static readonly Error Locked = Error.Conflict("ReviewerEvaluation.Locked", "Phiếu đã chốt; không thể sửa hoặc chốt lại bằng ý định mới.");
    public static readonly Error AssignmentVersionConflict = Error.Conflict("ReviewerEvaluation.AssignmentVersionConflict", "Phân công đã thay đổi. Vui lòng tải lại dữ liệu.");
    public static readonly Error VersionConflict = Error.Conflict("ReviewerEvaluation.VersionConflict", "Phiếu đã thay đổi hoặc phiên bản phiếu không phù hợp. Vui lòng tải lại dữ liệu.");
    public static readonly Error BatchNotOpen = Error.Conflict("ReviewerEvaluation.BatchNotOpen", "Chỉ nhập, sửa và chốt phiếu khi đợt đang Open.");
    public static readonly Error NotEligible = Error.Conflict("ReviewerEvaluation.NotEligible", "Phân công, tài khoản phản biện hoặc bản báo cáo không còn hợp lệ để ghi.");
    public static readonly Error ContextInvalid = Error.Conflict("ReviewerEvaluation.ContextInvalid", "Dữ liệu nhóm, đề tài hoặc đợt không nhất quán.");
    public static readonly Error IdempotencyConflict = Error.Conflict("ReviewerEvaluation.IdempotencyConflict", "Idempotency-Key đã được dùng cho một ý định khác.");
    public static readonly Error ConcurrencyConflict = Error.Conflict("ReviewerEvaluation.ConcurrencyConflict", "Có thay đổi đồng thời. Vui lòng tải lại dữ liệu trước ý định mới.");
}
