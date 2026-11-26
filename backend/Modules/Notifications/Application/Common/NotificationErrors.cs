using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.Notifications.Application.Common;

public static class NotificationErrors
{
    public static readonly Error AuthenticationRequired = Error.Unauthorized(
        "Notifications.AuthenticationRequired",
        "Bạn cần đăng nhập để truy cập thông báo.");

    public static readonly Error NotFound = Error.NotFound(
        "Notifications.NotFound",
        "Không tìm thấy thông báo.");

    public static readonly Error InvalidRequest = Error.Validation(
        "Notifications.InvalidRequest",
        "Dữ liệu yêu cầu thông báo không hợp lệ.");
}
