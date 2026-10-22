using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.TopicCatalog.Application.Common;

public static class TopicErrors
{
    public static readonly Error TopicNotFound = Error.NotFound(
        "Topic.NotFound",
        "Không tìm thấy đề tài yêu cầu.");

    public static readonly Error BatchNotFound = Error.NotFound(
        "Topic.BatchNotFound",
        "Không tìm thấy đợt khóa luận tốt nghiệp.");

    public static readonly Error AccessDenied = Error.Forbidden(
        "Topic.AccessDenied",
        "Bạn không có quyền thực hiện thao tác trên đề tài này.");

    public static readonly Error InvalidInput = Error.Validation(
        "Topic.InvalidInput",
        "Mã đề tài không hợp lệ.");

    public static readonly Error InvalidBatchId = Error.Validation(
        "Topic.InvalidBatchId",
        "Mã đợt khóa luận tốt nghiệp không hợp lệ.");

    public static readonly Error InvalidTitleVi = Error.Validation(
        "Topic.InvalidTitleVi",
        "Tên đề tài tiếng Việt không được để trống và không vượt quá 300 ký tự.");

    public static readonly Error InvalidTitleEn = Error.Validation(
        "Topic.InvalidTitleEn",
        "Tên đề tài tiếng Anh không được vượt quá 350 ký tự.");

    public static readonly Error InvalidStudentNumber = Error.Validation(
        "Topic.InvalidStudentNumber",
        "Chỉ tiêu sinh viên tối đa chỉ nhận giá trị 1 hoặc 2.");

    public static readonly Error DuplicateTitle = Error.Conflict(
        "Topic.DuplicateTitle",
        "Đề tài có cùng tên tiếng Việt đã tồn tại trong đợt khóa luận này của giảng viên.");

    public static readonly Error InvalidStatusTransition = Error.Conflict(
        "Topic.InvalidStatusTransition",
        "Không thể thực hiện thao tác ở trạng thái đề tài hiện tại.");

    public static readonly Error InvalidReviewDecision = Error.Validation(
        "Topic.InvalidReviewDecision",
        "Kết quả thẩm định không hợp lệ (chỉ chấp nhận: 'Approved', 'NeedRevision', 'Rejected').");

    public static readonly Error ReviewNoteRequired = Error.Validation(
        "Topic.ReviewNoteRequired",
        "Vui lòng nhập lý do khi từ chối hoặc yêu cầu chỉnh sửa đề tài.");

    public static readonly Error InvalidReviewNote = Error.Validation(
        "Topic.InvalidReviewNote",
        "Lý do thẩm định không được vượt quá 2000 ký tự.");

    public static readonly Error NoApprovedTopicsToPublish = Error.Validation(
        "Topic.NoApprovedTopicsToPublish",
        "Không có đề tài nào ở trạng thái đã duyệt (Approved) để công bố.");

    public static readonly Error InvalidRegistrationStatusFilter = Error.Validation(
        "Topic.InvalidRegistrationStatusFilter",
        "Bộ lọc trạng thái đăng ký không hợp lệ (chỉ chấp nhận: 'All', 'Empty', 'Registered', 'Full').");

    public static readonly Error InvalidTopicStatusFilter = Error.Validation(
        "Topic.InvalidTopicStatusFilter",
        "Bộ lọc trạng thái đề tài không hợp lệ.");
}
