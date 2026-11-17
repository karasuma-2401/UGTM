using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI;

public static class PlagiarismScanErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "PlagiarismScan.NotFound",
        "Không tìm thấy dữ liệu kiểm tra tương đồng cho báo cáo này.");

    public static readonly Error AccessDenied = Error.Forbidden(
        "PlagiarismScan.AccessDenied",
        "Bạn không có quyền truy cập báo cáo đối chiếu tương đồng này.");

    public static readonly Error ReportNotReady = Error.Conflict(
        "PlagiarismScan.ReportNotReady",
        "Báo cáo đối chiếu tương đồng đang được xử lý hoặc đang chờ cụm tính toán GPU.");

    public static readonly Error UnsupportedFormat = Error.Validation(
        "PlagiarismScan.UnsupportedFormat",
        "Tệp nộp định dạng .doc cũ không được dịch vụ AI hỗ trợ phân tích.");

    public static readonly Error RescanNotAllowed = Error.Forbidden(
        "PlagiarismScan.RescanNotAllowed",
        "Chỉ Giảng viên hướng dẫn được phân công hoặc Quản trị viên mới có quyền yêu cầu quét lại.");

    public static readonly Error CouncilNotReady = Error.Forbidden(
        "PlagiarismScan.CouncilNotReady",
        "Hội đồng chỉ có thể xem báo cáo khi bài nộp đã chuyển sang giai đoạn chấm thẩm định.");
}

