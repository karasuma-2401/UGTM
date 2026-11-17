using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.GetMidtermReportReviewQueue;

public static class MidtermReportReviewQueueErrors
{
    public static readonly Error AuthenticationRequired = Error.Unauthorized(
        "MidtermReportReview.AuthenticationRequired", "Vui lòng đăng nhập để xem danh sách hồ sơ báo cáo giữa kỳ.");
    public static readonly Error AccessDenied = Error.Forbidden(
        "MidtermReportReview.AccessDenied", "Bạn không có quyền xem danh sách hồ sơ báo cáo giữa kỳ.");
    public static readonly Error InvalidRequest = Error.Validation(
        "MidtermReportReview.InvalidRequest", "Đợt hoặc trạng thái lọc hồ sơ báo cáo giữa kỳ không hợp lệ.");
    public static readonly Error InconsistentData = Error.Failure(
        "MidtermReportReview.InconsistentData", "Không thể đọc danh sách hồ sơ báo cáo giữa kỳ do dữ liệu liên kết không hợp lệ.");
}
