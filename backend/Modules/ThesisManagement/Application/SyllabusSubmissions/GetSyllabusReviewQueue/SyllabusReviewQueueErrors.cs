using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusReviewQueue;

public static class SyllabusReviewQueueErrors
{
    public static readonly Error AuthenticationRequired = Error.Unauthorized(
        "SyllabusReview.AuthenticationRequired", "Vui lòng đăng nhập để xem danh sách hồ sơ đề cương.");
    public static readonly Error AccessDenied = Error.Forbidden(
        "SyllabusReview.AccessDenied", "Bạn không có quyền xem danh sách hồ sơ đề cương.");
    public static readonly Error InvalidRequest = Error.Validation(
        "SyllabusReview.InvalidRequest", "Đợt hoặc trạng thái lọc hồ sơ đề cương không hợp lệ.");
    public static readonly Error InconsistentData = Error.Failure(
        "SyllabusReview.InconsistentData", "Không thể đọc danh sách hồ sơ đề cương do dữ liệu liên kết không hợp lệ.");
}
