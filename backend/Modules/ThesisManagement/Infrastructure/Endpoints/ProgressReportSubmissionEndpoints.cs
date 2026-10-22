using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.GetProgressReport;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.GetProgressReportFileDownload;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.ReviewProgressReport;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.SubmitProgressReport;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class ProgressReportSubmissionEndpoints
{
    private const long MultipartOverheadBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapProgressReportSubmissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var report = endpoints.MapGroup("/api/thesis/progress-milestones/{milestoneId:guid}/report")
            .WithTags("ThesisManagement.ProgressReports")
            .RequireAuthorization();

        report.MapPost("", SubmitAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(ThesisSubmissionFileValidator.MaximumFileSizeBytes + MultipartOverheadBytes))
            .Accepts<IFormFile>("multipart/form-data")
            .WithSummary("Nộp báo cáo tiến độ theo Milestone")
            .WithDescription("""
                Mọi sinh viên thuộc nhóm của Milestone đều có thể nộp thay nhóm bằng `multipart/form-data` gồm
                `file`, `gitUrl` và `studentNote` tùy chọn. Tệp chỉ nhận PDF, DOC hoặc DOCX tối đa 20 MiB; backend
                kiểm tra extension, kích thước và chữ ký nội dung trước khi lưu private vào S3/MinIO. `gitUrl` phải
                là URL tuyệt đối `http` hoặc `https`; hệ thống không gọi URL này để tránh SSRF.

                Lần nộp đầu tạo v1. Sau khi GVHD chọn `NeedRevision`, nhóm có thể nộp v2, v3...; mọi phiên bản,
                tệp, Git URL và ghi chú cũ đều được giữ. `isLate` được tính ở server: chỉ là `true` khi thời điểm
                nhận thực tế muộn hơn deadline của Milestone.

                Yêu cầu Bearer token của thành viên nhóm, sau khi nhóm đang `InProgress`.
                - 200 OK: nộp thành công.
                - 400 Bad Request: file, Git URL hoặc request không hợp lệ.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không thuộc nhóm của Milestone.
                - 404 Not Found: không tìm thấy Milestone.
                - 409 Conflict: nhóm không ở trạng thái hợp lệ hoặc chưa được yêu cầu chỉnh sửa.
                - 413 Payload Too Large: multipart vượt giới hạn cho phép.
                """)
            .Produces<ApiResponse<ProgressReportSubmissionDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status413PayloadTooLarge);

        report.MapPost("/review", ReviewAsync)
            .WithSummary("GVHD đánh giá phiên bản báo cáo tiến độ hiện tại")
            .WithDescription("""
                Chỉ GVHD hiện được phân công cho đề tài hoặc `Admin` có thể đánh giá phiên bản đang `Submitted`.
                `decision` nhận `Accepted` hoặc `NeedRevision` (không phân biệt hoa thường); `feedback` là bắt buộc
                khi yêu cầu chỉnh sửa. `Accepted` khép lại Milestone, còn `NeedRevision` mở một lần nộp mới v2, v3...
                mà không thay đổi các phiên bản hoặc nhận xét đã có.

                Mỗi quyết định được audit. Đây là quy trình đánh giá của GVHD, độc lập với thẩm định Cổng 1/Cổng 2
                do Khoa thực hiện.

                - 200 OK: đánh giá thành công.
                - 400 Bad Request: quyết định hoặc feedback không hợp lệ.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không phải GVHD được gán hoặc Admin.
                - 404 Not Found: không tìm thấy Milestone hoặc báo cáo.
                - 409 Conflict: phiên bản không ở trạng thái chờ đánh giá hoặc có xung đột đồng thời.
                """)
            .Produces<ApiResponse<ProgressReportReviewDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        report.MapGet("", GetAsync)
            .WithSummary("Xem lịch sử báo cáo tiến độ theo Milestone")
            .WithDescription("""
                Trả toàn bộ lịch sử v1, v2... gồm trạng thái, Git URL, ghi chú, cờ nộp trễ, metadata tệp và
                nhận xét của GVHD. Thành viên nhóm, GVHD được gán, Khoa có quyền thẩm định hoặc `Admin` có thể xem.
                Response không chứa object key hay URL public; dùng endpoint tải riêng để nhận URL ký tạm thời.

                - 200 OK: lịch sử báo cáo.
                - 400 Bad Request: milestoneId không hợp lệ.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không có quyền xem.
                - 404 Not Found: không tìm thấy Milestone hoặc nhóm chưa nộp báo cáo.
                """)
            .Produces<ApiResponse<ProgressReportHistoryDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/thesis/progress-report-files/{fileId:guid}/download", DownloadAsync)
            .WithTags("ThesisManagement.ProgressReports")
            .RequireAuthorization()
            .WithSummary("Lấy URL tải tạm thời cho tệp báo cáo tiến độ")
            .WithDescription("""
                Sau khi kiểm tra quyền, trả presigned URL chỉ có hiệu lực 5 phút cho tệp báo cáo tiến độ. Thành viên
                của nhóm, GVHD được gán, Khoa có quyền thẩm định hoặc `Admin` mới được tải. Bucket MinIO/S3 luôn
                private và object key nội bộ không xuất hiện trong response.
                """)
            .Produces<ApiResponse<SubmissionFileDownloadDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> SubmitAsync(
        Guid milestoneId,
        [FromForm(Name = "file")] IFormFile? file,
        [FromForm(Name = "gitUrl")] string? gitUrl,
        [FromForm(Name = "studentNote")] string? studentNote,
        SubmitProgressReportHandler handler,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Results.Json(
                ApiResponse<ProgressReportSubmissionDto>.Fail(
                    ApiError.Create("ProgressReport.InvalidFile", "Phải cung cấp tệp báo cáo tiến độ.")),
                statusCode: StatusCodes.Status400BadRequest);
        }

        await using var stream = file.OpenReadStream();
        return (await handler.HandleAsync(
            new SubmitProgressReportCommand(milestoneId, file.FileName, stream, gitUrl, studentNote),
            cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> ReviewAsync(
        Guid milestoneId,
        [FromBody] ReviewProgressReportRequest? request,
        ReviewProgressReportHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new ReviewProgressReportCommand(milestoneId, request?.Decision, request?.Feedback),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetAsync(
        Guid milestoneId,
        GetProgressReportHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(new GetProgressReportQuery(milestoneId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> DownloadAsync(
        Guid fileId,
        GetProgressReportFileDownloadHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(new GetProgressReportFileDownloadQuery(fileId), cancellationToken)).ToHttpResult();

    public sealed record ReviewProgressReportRequest(string? Decision, string? Feedback);
}
