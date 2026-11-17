using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class MidtermReportSubmissionErrors
{
    public static readonly Error AccessDenied = Error.Forbidden(
        "MidtermReport.AccessDenied",
        "Bạn không có quyền truy cập báo cáo giữa kỳ của nhóm này.");

    public static readonly Error ReviewAccessDenied = Error.Forbidden(
        "MidtermReport.ReviewAccessDenied",
        "Bạn không có quyền thẩm định báo cáo giữa kỳ.");

    public static readonly Error GroupNotFound = Error.NotFound(
        "MidtermReport.GroupNotFound",
        "Không tìm thấy nhóm khóa luận.");

    public static readonly Error SubmissionNotFound = Error.NotFound(
        "MidtermReport.NotFound",
        "Nhóm chưa có báo cáo giữa kỳ để thẩm định.");

    public static readonly Error GateConfigurationNotFound = Error.Conflict(
        "MidtermReport.GateConfigurationNotFound",
        "Cổng thẩm định báo cáo giữa kỳ chưa được cấu hình deadline cho đợt khóa luận này.");

    public static readonly Error InitialSubmissionDeadlinePassed = Error.Conflict(
        "MidtermReport.InitialSubmissionDeadlinePassed",
        "Đã qua hạn nộp báo cáo giữa kỳ lần đầu.");

    public static readonly Error FacultyResponseDeadlinePassed = Error.Conflict(
        "MidtermReport.FacultyResponseDeadlinePassed",
        "Đã qua hạn Khoa phản hồi báo cáo giữa kỳ lần đầu.");

    public static readonly Error FinalClosureDeadlinePassed = Error.Conflict(
        "MidtermReport.FinalClosureDeadlinePassed",
        "Đã qua hạn chốt cuối cùng của Cổng 2.");

    public static readonly Error InvalidSubmissionState = Error.Conflict(
        "MidtermReport.InvalidSubmissionState",
        "Trạng thái hiện tại không cho phép nộp báo cáo giữa kỳ.");

    public static readonly Error InvalidReviewState = Error.Conflict(
        "MidtermReport.InvalidReviewState",
        "Trạng thái hiện tại không cho phép thẩm định báo cáo giữa kỳ.");

    public static readonly Error InvalidFile = Error.Validation(
        "MidtermReport.InvalidFile",
        "Tệp báo cáo giữa kỳ không hợp lệ. Chỉ chấp nhận PDF, DOC hoặc DOCX tối đa 20 MB với nội dung khớp định dạng.");

    public static readonly Error InvalidRequest = Error.Validation(
        "MidtermReport.InvalidRequest",
        "Thông tin nộp hoặc thẩm định báo cáo giữa kỳ không hợp lệ.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "MidtermReport.ConcurrencyConflict",
        "Báo cáo giữa kỳ vừa được một yêu cầu khác cập nhật. Vui lòng tải lại và thử lại.");
}
