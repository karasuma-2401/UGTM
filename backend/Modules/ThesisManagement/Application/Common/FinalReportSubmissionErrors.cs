using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class FinalReportSubmissionErrors
{
    public static readonly Error InvalidFile = Error.Validation(
        "FinalReport.InvalidFile",
        "Tệp báo cáo cuối kỳ không hợp lệ. Chỉ chấp nhận PDF, DOC hoặc DOCX tối đa 20 MiB với nội dung khớp định dạng.");

    public static readonly Error EvaluationStarted = Error.Conflict(
        "FinalReport.EvaluationStarted",
        "Báo cáo cuối kỳ đã bắt đầu chấm; không thể nộp lại.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "FinalReport.ConcurrencyConflict",
        "Dữ liệu nhóm hoặc báo cáo cuối kỳ vừa được thay đổi. Vui lòng tải lại và thử lại.");

    public static readonly Error AuthenticationRequired = Error.Unauthorized(
        "FinalReport.AuthenticationRequired",
        "Bạn cần đăng nhập để truy cập báo cáo cuối kỳ.");

    public static readonly Error AccessDenied = Error.Forbidden(
        "FinalReport.AccessDenied",
        "Chỉ sinh viên là thành viên đã xác nhận của nhóm mới được nộp báo cáo cuối kỳ.");

    public static readonly Error InvalidRequest = Error.Validation(
        "FinalReport.InvalidRequest",
        "Thông tin yêu cầu báo cáo cuối kỳ không hợp lệ.");

    public static readonly Error ReadAccessDenied = Error.Forbidden(
        "FinalReport.AccessDenied",
        "Bạn không có quyền xem hoặc tải báo cáo cuối kỳ của nhóm này.");

    public static readonly Error ReportNotFound = Error.NotFound(
        "FinalReport.ReportNotFound",
        "Không tìm thấy báo cáo cuối kỳ.");

    public static readonly Error EvaluationAccessDenied = Error.Forbidden(
        "FinalReport.EvaluationAccessDenied",
        "Chỉ GVHD đang được phân công hoặc Admin được bắt đầu chấm báo cáo cuối kỳ.");

    public static readonly Error InvalidEvaluationGroupState = Error.Conflict(
        "FinalReport.InvalidGroupState",
        "Nhóm phải ở trạng thái đã nộp báo cáo cuối kỳ để bắt đầu chấm.");

    public static readonly Error InvalidEvaluationState = Error.Conflict(
        "FinalReport.InvalidEvaluationState",
        "Chỉ được bắt đầu chấm báo cáo InitialFinal đang ở trạng thái Submitted.");

    public static readonly Error FileNotFound = Error.NotFound(
        "FinalReport.FileNotFound",
        "Không tìm thấy tệp báo cáo cuối kỳ.");

    public static readonly Error GroupNotFound = Error.NotFound(
        "FinalReport.GroupNotFound",
        "Không tìm thấy nhóm khóa luận.");

    public static readonly Error InvalidGroupState = Error.Conflict(
        "FinalReport.InvalidGroupState",
        "Trạng thái nhóm hiện tại không cho phép nộp báo cáo cuối kỳ.");

    public static readonly Error MidtermNotApproved = Error.Conflict(
        "FinalReport.MidtermNotApproved",
        "Nhóm phải được duyệt báo cáo giữa kỳ trước khi nộp báo cáo cuối kỳ.");

    public static readonly Error BatchNotFound = Error.NotFound(
        "FinalReport.BatchNotFound",
        "Không tìm thấy đợt khóa luận của nhóm.");

    public static readonly Error OutsideAllowedWindow = Error.Conflict(
        "FinalReport.OutsideAllowedWindow",
        "Chỉ được nộp báo cáo cuối kỳ khi đợt khóa luận đang mở và trong thời gian FINAL_EVALUATION đã cấu hình.");
}
