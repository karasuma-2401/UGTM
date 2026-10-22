using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.AcademicPeriods.Application.Common;

public static class AcademicBatchErrors
{
    public static readonly Error AccessDenied = Error.Forbidden(
        "AcademicBatch.AccessDenied",
        "Bạn không có quyền thực hiện thao tác này.");

    public static readonly Error NotFound = Error.NotFound(
        "AcademicBatch.NotFound",
        "Không tìm thấy đợt khóa luận.");

    public static readonly Error ActiveTemplateNotFound = Error.Conflict(
        "AcademicBatch.ActiveTemplateNotFound",
        "Không tìm thấy phiên bản lịch mẫu đang hoạt động.");

    public static readonly Error InvalidRequest = Error.Validation(
        "AcademicBatch.InvalidRequest",
        "Thông tin đợt khóa luận hoặc lịch giai đoạn không hợp lệ.");

    public static readonly Error DuplicateAcademicPeriod = Error.Conflict(
        "AcademicBatch.DuplicateAcademicPeriod",
        "Năm học và học kỳ này đã có một đợt khóa luận.");

    public static readonly Error InvalidStatusTransition = Error.Conflict(
        "AcademicBatch.InvalidStatusTransition",
        "Không thể chuyển trạng thái đợt khóa luận theo yêu cầu.");

    public static readonly Error ScoreConfigurationRequired = Error.Conflict(
        "AcademicBatch.ScoreConfigurationRequired",
        "Cần công bố một cấu hình điểm đang hoạt động trước khi mở đợt.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "AcademicBatch.ConcurrencyConflict",
        "Đợt khóa luận đã được cập nhật bởi một yêu cầu khác. Vui lòng tải lại dữ liệu và thử lại.");
}
