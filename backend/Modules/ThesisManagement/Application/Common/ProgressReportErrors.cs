using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class ProgressReportErrors
{
    public static readonly Error AccessDenied = Error.Forbidden(
        "ProgressReport.AccessDenied",
        "Bạn không có quyền truy cập hoặc thao tác báo cáo tiến độ này.");

    public static readonly Error ReviewAccessDenied = Error.Forbidden(
        "ProgressReport.ReviewAccessDenied",
        "Chỉ GVHD được phân công cho đề tài hoặc Admin mới có quyền đánh giá báo cáo tiến độ.");

    public static readonly Error MilestoneNotFound = Error.NotFound(
        "ProgressReport.MilestoneNotFound",
        "Không tìm thấy mốc tiến độ.");

    public static readonly Error ReportNotFound = Error.NotFound(
        "ProgressReport.NotFound",
        "Nhóm chưa nộp báo cáo cho mốc tiến độ này.");

    public static readonly Error InvalidRequest = Error.Validation(
        "ProgressReport.InvalidRequest",
        "Thông tin báo cáo tiến độ không hợp lệ.");

    public static readonly Error InvalidFile = Error.Validation(
        "ProgressReport.InvalidFile",
        "Tệp báo cáo tiến độ không hợp lệ. Chỉ chấp nhận PDF, DOC hoặc DOCX tối đa 20 MB với nội dung khớp định dạng.");

    public static readonly Error InvalidSubmissionState = Error.Conflict(
        "ProgressReport.InvalidSubmissionState",
        "Trạng thái hiện tại không cho phép nộp báo cáo tiến độ.");

    public static readonly Error InvalidReviewState = Error.Conflict(
        "ProgressReport.InvalidReviewState",
        "Trạng thái hiện tại không cho phép đánh giá báo cáo tiến độ.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "ProgressReport.ConcurrencyConflict",
        "Báo cáo tiến độ vừa được một yêu cầu khác cập nhật. Vui lòng tải lại và thử lại.");
}
