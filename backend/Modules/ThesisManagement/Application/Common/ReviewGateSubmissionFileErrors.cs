using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class ReviewGateSubmissionFileErrors
{
    public static readonly Error AccessDenied = Error.Forbidden(
        "ReviewGateSubmissionFile.AccessDenied",
        "Bạn không có quyền truy cập tệp của hồ sơ này.");

    public static readonly Error FileNotFound = Error.NotFound(
        "ReviewGateSubmissionFile.NotFound",
        "Không tìm thấy tệp của hồ sơ.");

    public static readonly Error InvalidRequest = Error.Validation(
        "ReviewGateSubmissionFile.InvalidRequest",
        "Yêu cầu tải tệp không hợp lệ.");
}
