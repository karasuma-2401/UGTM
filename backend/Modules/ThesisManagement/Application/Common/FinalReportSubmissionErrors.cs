using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class FinalReportSubmissionErrors
{
    public static readonly Error AuthenticationRequired = Error.Unauthorized(
        "FinalReport.AuthenticationRequired",
        "Bạn cần đăng nhập để nộp báo cáo cuối kỳ.");

    public static readonly Error AccessDenied = Error.Forbidden(
        "FinalReport.AccessDenied",
        "Chỉ sinh viên là thành viên đã xác nhận của nhóm mới được nộp báo cáo cuối kỳ.");

    public static readonly Error InvalidRequest = Error.Validation(
        "FinalReport.InvalidRequest",
        "Thông tin nộp báo cáo cuối kỳ không hợp lệ.");

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
