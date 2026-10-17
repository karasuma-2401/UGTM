using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.TopicCatalog.Application.Common;

public static class TopicErrors
{
    public static readonly Error TopicNotFound = Error.NotFound(
        "Topic.NotFound",
        "Không tìm thấy đề tài yêu cầu.");

    public static readonly Error AccessDenied = Error.Forbidden(
        "Topic.AccessDenied",
        "Bạn không có quyền xem thông tin theo dõi của đề tài này. Chỉ GVHD phụ trách hoặc Quản trị viên/Hội đồng mới có quyền truy cập.");

    public static readonly Error InvalidInput = Error.Validation(
        "Topic.InvalidInput",
        "Mã đề tài không hợp lệ.");

    public static readonly Error InvalidRegistrationStatusFilter = Error.Validation(
        "Topic.InvalidRegistrationStatusFilter",
        "Bộ lọc trạng thái đăng ký không hợp lệ (chỉ chấp nhận: 'All', 'Empty', 'Registered', 'Full').");

    public static readonly Error InvalidTopicStatusFilter = Error.Validation(
        "Topic.InvalidTopicStatusFilter",
        "Bộ lọc trạng thái đề tài không hợp lệ.");
}
