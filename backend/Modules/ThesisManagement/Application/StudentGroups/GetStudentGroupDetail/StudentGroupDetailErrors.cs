using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.StudentGroups.GetStudentGroupDetail;

public static class StudentGroupDetailErrors
{
    public static readonly Error AuthenticationRequired = Error.Unauthorized(
        "StudentGroup.AuthenticationRequired", "Vui lòng đăng nhập để xem thông tin nhóm khóa luận.");

    public static readonly Error InvalidRequest = Error.Validation(
        "StudentGroup.InvalidRequest", "Mã định danh nhóm không hợp lệ.");

    public static readonly Error GroupNotFound = Error.NotFound(
        "StudentGroup.NotFound", "Không tìm thấy nhóm khóa luận.");

    public static readonly Error AccessDenied = Error.Forbidden(
        "StudentGroup.AccessDenied", "Bạn không có quyền xem thông tin nhóm khóa luận này.");

    public static readonly Error InconsistentData = Error.Failure(
        "StudentGroup.InconsistentData", "Không thể đọc thông tin nhóm khóa luận do dữ liệu liên kết không hợp lệ.");
}
