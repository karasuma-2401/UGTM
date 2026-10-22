using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class ReviewGateConfigurationErrors
{
    public static readonly Error AccessDenied = Error.Forbidden(
        "ReviewGateConfiguration.AccessDenied",
        "Bạn không có quyền cấu hình thời hạn các cổng thẩm định.");

    public static readonly Error BatchNotFound = Error.NotFound(
        "ReviewGateConfiguration.BatchNotFound",
        "Không tìm thấy đợt khóa luận.");

    public static readonly Error InvalidRequest = Error.Validation(
        "ReviewGateConfiguration.InvalidRequest",
        "Thông tin cổng thẩm định hoặc các mốc thời hạn không hợp lệ.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "ReviewGateConfiguration.ConcurrencyConflict",
        "Cấu hình cổng thẩm định đã được cập nhật bởi một yêu cầu khác. Vui lòng tải lại và thử lại.");
}
