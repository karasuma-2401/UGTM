using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.ReviewMidtermReport;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.SubmitMidtermReport;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class MidtermReportSubmissionEndpoints
{
    private const string ReviewPolicy = "Permission:" + PermissionCodes.ThesisReviewGatesReview;
    private const long MultipartOverheadBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapMidtermReportSubmissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/thesis/groups/{groupId:guid}/midterm-report")
            .WithTags("ThesisManagement.MidtermReport");

        group.MapPost("/submissions", SubmitAsync)
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(
                ThesisSubmissionFileValidator.MaximumFileSizeBytes + MultipartOverheadBytes))
            .Accepts<IFormFile>("multipart/form-data")
            .WithSummary("Nộp báo cáo giữa kỳ hoặc phiên bản chỉnh sửa")
            .WithDescription("""
                Mọi sinh viên là thành viên của nhóm đều có thể nộp thay mặt nhóm bằng `multipart/form-data`
                với trường `file`. Tệp chỉ chấp nhận `.pdf`, `.doc` hoặc `.docx`, tối đa 20 MiB;
                hệ thống kiểm tra cả tên mở rộng, kích thước và chữ ký nội dung trước khi lưu vào object storage private.

                Lần đầu chỉ được nộp không muộn hơn `initialSubmissionDeadlineUtc`. Sau khi Khoa yêu cầu
                chỉnh sửa, nhóm có thể nộp v2, v3... đến `finalClosureDeadlineUtc`. Không thể nộp lại khi
                hồ sơ đang chờ thẩm định, đã duyệt hoặc đã bị từ chối. Mỗi phiên bản giữ object riêng, không
                ghi đè tệp hay nhận xét cũ.

                Yêu cầu Bearer token của thành viên chính nhóm, sau khi nhóm đã qua Cổng 1 và đang `InProgress`.
                - 200 OK: phiên bản đã nộp thành công.
                - 400 Bad Request: tệp hoặc request không hợp lệ.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không thuộc nhóm.
                - 404 Not Found: không tìm thấy nhóm.
                - 409 Conflict: chưa cấu hình Cổng 2, trễ hạn hoặc trạng thái không cho phép nộp.
                - 413 Payload Too Large: multipart vượt giới hạn cho phép.
                """)
            .Produces<ApiResponse<MidtermReportSubmissionDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status413PayloadTooLarge);

        group.MapPost("/review", ReviewAsync)
            .RequireAuthorization(ReviewPolicy)
            .WithSummary("Thẩm định phiên bản báo cáo giữa kỳ hiện tại")
            .WithDescription("""
                Khoa hoặc Admin thẩm định phiên bản báo cáo giữa kỳ hiện đang ở trạng thái `Submitted`.
                `decision` chỉ nhận `Approved`, `NeedRevision` hoặc `Rejected` (không phân biệt hoa thường).
                `feedback` là bắt buộc khi chọn `NeedRevision` hoặc `Rejected`.

                `Approved` đưa nhóm trở lại `InProgress` để tiếp tục các công việc tiếp theo. `NeedRevision`
                mở quyền nộp phiên bản mới, còn `Rejected` chuyển nhóm sang `Stopped`. Quyết định và feedback
                được lưu theo từng version, không ghi đè lịch sử. Phiên bản đầu phải được phản hồi trước
                `facultyResponseDeadlineUtc`; mọi quyết định phải hoàn tất trước `finalClosureDeadlineUtc`.

                Yêu cầu Bearer token có permission `thesis.review-gates.review`, được cấp mặc định cho
                `Faculty` và `Admin`.
                - 200 OK: thẩm định thành công.
                - 400 Bad Request: quyết định hoặc feedback không hợp lệ.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không có quyền thẩm định.
                - 404 Not Found: không tìm thấy nhóm hoặc hồ sơ.
                - 409 Conflict: deadline đã qua hoặc trạng thái không cho phép thẩm định.
                """)
            .Produces<ApiResponse<SubmissionReviewDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> SubmitAsync(
        Guid groupId,
        [FromForm(Name = "file")] IFormFile? file,
        SubmitMidtermReportHandler handler,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Results.Json(
                ApiResponse<MidtermReportSubmissionDto>.Fail(
                    ApiError.Create("MidtermReport.InvalidFile", "Phải cung cấp tệp báo cáo giữa kỳ.")),
                statusCode: StatusCodes.Status400BadRequest);
        }

        await using var stream = file.OpenReadStream();
        return (await handler.HandleAsync(
            new SubmitMidtermReportCommand(groupId, file.FileName, stream),
            cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> ReviewAsync(
        Guid groupId,
        [FromBody] ReviewMidtermReportRequest? request,
        ReviewMidtermReportHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new ReviewMidtermReportCommand(groupId, request?.Decision ?? string.Empty, request?.Feedback),
            cancellationToken)).ToHttpResult();

    public sealed record ReviewMidtermReportRequest(string? Decision, string? Feedback);
}
