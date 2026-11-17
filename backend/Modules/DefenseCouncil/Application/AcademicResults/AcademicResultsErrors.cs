using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.DefenseCouncil.Application.AcademicResults;

public static class AcademicResultsErrors
{
    public static Error AuthenticationRequired =>
        Error.Forbidden("AcademicResults.AuthenticationRequired", "Yêu cầu xác thực để thực hiện thao tác.");

    public static Error Forbidden =>
        Error.Forbidden("AcademicResults.Forbidden", "Bạn không có quyền truy cập kết quả khóa luận tốt nghiệp.");

    public static Error InvalidBatchId =>
        Error.Validation("AcademicResults.InvalidBatchId", "Mã đợt khóa luận (batchId) là bắt buộc và không được để trống.");

    public static Error InvalidPhase =>
        Error.Validation("AcademicResults.InvalidPhase", "Giai đoạn (phase) bắt buộc phải là 'Midterm' hoặc 'Final'.");

    public static Error InvalidStatus =>
        Error.Validation("AcademicResults.InvalidStatus", "Trạng thái lọc (status) không hợp lệ.");

    public static Error InvalidFormat =>
        Error.Validation("AcademicResults.InvalidFormat", "Định dạng xuất báo cáo (format) bắt buộc phải là 'xlsx' hoặc 'pdf'.");

    public static Error GroupNotFound =>
        Error.NotFound("AcademicResults.GroupNotFound", "Không tìm thấy hồ sơ nhóm khóa luận.");
}
