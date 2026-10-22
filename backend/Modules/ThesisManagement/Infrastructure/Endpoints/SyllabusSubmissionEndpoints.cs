using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusSubmissionHistory;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.GetMidtermReportSubmissionHistory;
using UGTM.Modules.ThesisManagement.Application.ReviewGateSubmissions.GetReviewGateSubmissionFileDownload;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.ReviewSyllabus;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.SubmitSyllabus;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class SyllabusSubmissionEndpoints
{
    private const string ReviewPolicy = "Permission:" + PermissionCodes.ThesisReviewGatesReview;
    private const long MultipartOverheadBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapSyllabusSubmissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/thesis/groups/{groupId:guid}/syllabus")
            .WithTags("ThesisManagement.Syllabus");

        group.MapPost("/submissions", SubmitAsync)
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(
                ThesisSubmissionFileValidator.MaximumFileSizeBytes + MultipartOverheadBytes))
            .Accepts<IFormFile>("multipart/form-data")
            .WithSummary("Nộp đề cương chi tiết hoặc phiên bản chỉnh sửa")
            .WithDescription("""
                Mọi sinh viên là thành viên của nhóm đều có thể nộp thay mặt nhóm bằng `multipart/form-data`
                với trường `file`. Tệp chỉ chấp nhận `.pdf`, `.doc` hoặc `.docx`, tối đa 20 MiB;
                hệ thống kiểm tra cả tên mở rộng, kích thước và chữ ký nội dung trước khi lưu vào object storage private.

                Lần đầu chỉ được nộp không muộn hơn `initialSubmissionDeadlineUtc`. Sau khi Khoa yêu cầu
                chỉnh sửa, nhóm có thể nộp v2, v3... đến `finalClosureDeadlineUtc`. Không thể nộp lại khi
                hồ sơ đang chờ thẩm định, đã duyệt hoặc đã bị từ chối. Mỗi phiên bản giữ object riêng, không
                ghi đè tệp hay nhận xét cũ.

                Yêu cầu Bearer token của thành viên chính nhóm.
                - 200 OK: phiên bản đã nộp thành công.
                - 400 Bad Request: tệp hoặc request không hợp lệ.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không thuộc nhóm.
                - 404 Not Found: không tìm thấy nhóm.
                - 409 Conflict: chưa cấu hình Cổng 1, trễ hạn hoặc trạng thái không cho phép nộp.
                - 413 Payload Too Large: multipart vượt giới hạn cho phép.
                """)
            .Produces<ApiResponse<SyllabusSubmissionDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status413PayloadTooLarge);

        group.MapPost("/review", ReviewAsync)
            .RequireAuthorization(ReviewPolicy)
            .WithSummary("Thẩm định phiên bản đề cương hiện tại")
            .WithDescription("""
                Khoa hoặc Admin thẩm định phiên bản đề cương hiện đang ở trạng thái `Submitted`.
                `decision` chỉ nhận `Approved`, `NeedRevision` hoặc `Rejected` (không phân biệt hoa thường).
                `feedback` là bắt buộc khi chọn `NeedRevision` hoặc `Rejected`.

                `Approved` chuyển nhóm sang `InProgress` để mở các Milestone sau này. `NeedRevision` mở
                quyền nộp phiên bản mới, còn `Rejected` chuyển nhóm sang `Stopped`. Quyết định và feedback
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

        endpoints.MapGet("/api/thesis/groups/{groupId:guid}/submissions/{gateType}", GetHistoryAsync)
            .WithTags("ThesisManagement.ReviewGates")
            .RequireAuthorization()
            .WithSummary("Xem lịch sử các phiên bản hồ sơ tại Cổng 1 hoặc Cổng 2")
            .WithDescription("""
                Đặt `gateType` là `Syllabus` (Cổng 1 — đề cương chi tiết) hoặc `Midterm` (Cổng 2 — báo cáo
                giữa kỳ). Response trả metadata tệp và kết quả thẩm định theo từng phiên bản. Thành viên của
                nhóm chỉ xem được nhóm mình; `Faculty`/`Admin` có quyền thẩm định có thể tra cứu để xử lý hồ sơ.
                Response không chứa object key hoặc public URL; endpoint tải tệp có kiểm soát sẽ được cung cấp
                riêng để giữ bucket MinIO/S3 private.

                Yêu cầu Bearer token.
                - 200 OK: lịch sử phiên bản và review.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không thuộc nhóm và không có quyền thẩm định.
                - 400 Bad Request: `gateType` không phải `Syllabus` hoặc `Midterm`.
                - 404 Not Found: không tìm thấy nhóm hoặc nhóm chưa có hồ sơ tương ứng.
                """)
            .Produces<ApiResponse<SyllabusSubmissionHistoryDto>>()
            .Produces<ApiResponse<MidtermReportSubmissionHistoryDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/thesis/submission-files/{fileId:guid}/download", GetDownloadAsync)
            .WithTags("ThesisManagement.ReviewGates")
            .RequireAuthorization()
            .WithSummary("Lấy URL tải tạm thời cho tệp hồ sơ Cổng 1 hoặc Cổng 2")
            .WithDescription("""
                Sau khi kiểm tra quyền trên database, trả về presigned URL chỉ có hiệu lực 5 phút cho một
                tệp thuộc Cổng 1 hoặc Cổng 2. Thành viên của chính nhóm hoặc `Faculty`/`Admin` có quyền thẩm định mới
                nhận được URL. Object key nội bộ và bucket private không bao giờ được trả về client.

                Yêu cầu Bearer token.
                - 200 OK: URL tải tạm thời và thời điểm hết hạn UTC.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không có quyền truy cập tệp.
                - 404 Not Found: không tìm thấy tệp hồ sơ.
                """)
            .Produces<ApiResponse<SubmissionFileDownloadDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> SubmitAsync(
        Guid groupId,
        [FromForm(Name = "file")] IFormFile? file,
        SubmitSyllabusHandler handler,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Results.Json(
                ApiResponse<SyllabusSubmissionDto>.Fail(
                    ApiError.Create("Syllabus.InvalidFile", "Phải cung cấp tệp đề cương.")),
                statusCode: StatusCodes.Status400BadRequest);
        }

        await using var stream = file.OpenReadStream();
        return (await handler.HandleAsync(
            new SubmitSyllabusCommand(groupId, file.FileName, stream),
            cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> ReviewAsync(
        Guid groupId,
        [FromBody] ReviewSyllabusRequest? request,
        ReviewSyllabusHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new ReviewSyllabusCommand(groupId, request?.Decision ?? string.Empty, request?.Feedback),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetHistoryAsync(
        Guid groupId,
        string gateType,
        GetSyllabusSubmissionHistoryHandler syllabusHandler,
        GetMidtermReportSubmissionHistoryHandler midtermHandler,
        CancellationToken cancellationToken)
    {
        if (string.Equals(gateType, "Syllabus", StringComparison.OrdinalIgnoreCase))
        {
            return (await syllabusHandler.HandleAsync(
                new GetSyllabusSubmissionHistoryQuery(groupId),
                cancellationToken)).ToHttpResult();
        }

        if (string.Equals(gateType, "Midterm", StringComparison.OrdinalIgnoreCase))
        {
            return (await midtermHandler.HandleAsync(
                new GetMidtermReportSubmissionHistoryQuery(groupId),
                cancellationToken)).ToHttpResult();
        }

        return Results.Json(
            ApiResponse<object>.Fail(
                ApiError.Create(
                    "ReviewGate.InvalidGateType",
                    "gateType chỉ nhận Syllabus hoặc Midterm.")),
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> GetDownloadAsync(
        Guid fileId,
        GetReviewGateSubmissionFileDownloadHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new GetReviewGateSubmissionFileDownloadQuery(fileId),
            cancellationToken)).ToHttpResult();

    public sealed record ReviewSyllabusRequest(string? Decision, string? Feedback);
}
