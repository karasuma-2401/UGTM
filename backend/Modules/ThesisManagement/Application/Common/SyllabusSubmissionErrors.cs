using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class SyllabusSubmissionErrors
{
    public static readonly Error AccessDenied = Error.Forbidden(
        "Syllabus.AccessDenied",
        "Bạn không có quyền truy cập hồ sơ đề cương của nhóm này.");

    public static readonly Error ReviewAccessDenied = Error.Forbidden(
        "Syllabus.ReviewAccessDenied",
        "Bạn không có quyền thẩm định đề cương.");

    public static readonly Error GroupNotFound = Error.NotFound(
        "Syllabus.GroupNotFound",
        "Không tìm thấy nhóm khóa luận.");

    public static readonly Error SubmissionNotFound = Error.NotFound(
        "Syllabus.NotFound",
        "Nhóm chưa có hồ sơ đề cương để thẩm định.");

    public static readonly Error FileNotFound = Error.NotFound(
        "Syllabus.FileNotFound",
        "Không tìm thấy tệp đề cương.");

    public static readonly Error GateConfigurationNotFound = Error.Conflict(
        "Syllabus.GateConfigurationNotFound",
        "Cổng thẩm định đề cương chưa được cấu hình deadline cho đợt khóa luận này.");

    public static readonly Error InitialSubmissionDeadlinePassed = Error.Conflict(
        "Syllabus.InitialSubmissionDeadlinePassed",
        "Đã qua hạn nộp đề cương lần đầu.");

    public static readonly Error FacultyResponseDeadlinePassed = Error.Conflict(
        "Syllabus.FacultyResponseDeadlinePassed",
        "Đã qua hạn Khoa phản hồi hồ sơ đề cương lần đầu.");

    public static readonly Error FinalClosureDeadlinePassed = Error.Conflict(
        "Syllabus.FinalClosureDeadlinePassed",
        "Đã qua hạn chốt cuối cùng của Cổng 1.");

    public static readonly Error InvalidSubmissionState = Error.Conflict(
        "Syllabus.InvalidSubmissionState",
        "Trạng thái hiện tại không cho phép nộp đề cương.");

    public static readonly Error InvalidReviewState = Error.Conflict(
        "Syllabus.InvalidReviewState",
        "Trạng thái hiện tại không cho phép thẩm định đề cương.");

    public static readonly Error InvalidFile = Error.Validation(
        "Syllabus.InvalidFile",
        "Tệp đề cương không hợp lệ. Chỉ chấp nhận PDF, DOC hoặc DOCX tối đa 20 MB với nội dung khớp định dạng.");

    public static readonly Error InvalidRequest = Error.Validation(
        "Syllabus.InvalidRequest",
        "Thông tin nộp hoặc thẩm định đề cương không hợp lệ.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "Syllabus.ConcurrencyConflict",
        "Hồ sơ đề cương vừa được một yêu cầu khác cập nhật. Vui lòng tải lại và thử lại.");
}
