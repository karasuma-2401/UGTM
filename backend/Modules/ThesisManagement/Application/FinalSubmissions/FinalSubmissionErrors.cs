using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.FinalSubmissions;

public static class FinalSubmissionErrors
{
    public static readonly Error AuthenticationRequired = Error.Unauthorized("FinalSubmission.AuthenticationRequired", "Bạn cần đăng nhập.");
    public static readonly Error AccessDenied = Error.Forbidden("FinalSubmission.AccessDenied", "Bạn không có quyền trên hồ sơ này.");
    public static readonly Error InvalidRequest = Error.Validation("FinalSubmission.InvalidRequest", "Yêu cầu không hợp lệ.");
    public static readonly Error InvalidFile = Error.Validation("FinalSubmission.InvalidFile", "Cần đúng PDF và DOCX hợp lệ, tối đa 20 MiB mỗi tệp.");
    public static readonly Error InvalidMetadata = Error.Validation("FinalSubmission.InvalidMetadata", "Cần đầy đủ Abstract/Keywords Việt–Anh, đúng giới hạn ký tự.");
    public static readonly Error NotFound = Error.NotFound("FinalSubmission.NotFound", "Không tìm thấy hồ sơ hoặc tệp.");
    public static readonly Error ContextInvalid = Error.Conflict("FinalSubmission.ContextInvalid", "Liên kết nhóm, đợt hoặc bằng chứng không còn hợp lệ.");
    public static readonly Error BatchNotWritable = Error.Conflict("FinalSubmission.BatchNotWritable", "Trạng thái đợt không cho phép thao tác này.");
    public static readonly Error WindowClosed = Error.Conflict("FinalSubmission.SubmissionWindowClosed", "Ngoài thời gian nộp lưu chiểu.");
    public static readonly Error DefenseNotLocked = Error.Conflict("FinalSubmission.DefenseNotLocked", "Cần biên bản bảo vệ Locked hợp lệ.");
    public static readonly Error VersionConflict = Error.Conflict("FinalSubmission.VersionConflict", "Hồ sơ đã thay đổi; vui lòng tải lại.");
    public static readonly Error Locked = Error.Conflict("FinalSubmission.Locked", "Hồ sơ đã xác nhận; không thể nộp lại.");
    public static readonly Error IdempotencyConflict = Error.Conflict("FinalSubmission.IdempotencyConflict", "Key đã dùng cho yêu cầu khác.");
    public static readonly Error ConcurrencyConflict = Error.Conflict("FinalSubmission.ConcurrencyConflict", "Dữ liệu đang thay đổi; vui lòng thử lại.");
    public static readonly Error InvalidState = Error.Conflict("FinalSubmission.InvalidState", "Hành động không phù hợp với bước xét duyệt hiện tại.");
    public static readonly Error Legacy = Error.Conflict("FinalSubmission.Legacy", "Hồ sơ cũ chưa có đủ binding của workflow lưu chiểu.");
}
