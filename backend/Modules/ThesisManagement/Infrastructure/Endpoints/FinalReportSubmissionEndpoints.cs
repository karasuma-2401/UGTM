using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.SubmitFinalReport;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.GetFinalReportHistory;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.GetFinalReportFileDownload;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.BeginFinalReportEvaluation;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class FinalReportSubmissionEndpoints
{
    private const long MaximumRequestSizeBytes = ThesisSubmissionFileValidator.MaximumFileSizeBytes + 64 * 1024;

    public static IEndpointRouteBuilder MapFinalReportSubmissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/thesis/final-reports", SubmitAsync)
            .WithTags("ThesisManagement.FinalReport")
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestSizeBytes))
            .Accepts<SubmitFinalReportRequest>("multipart/form-data")
            .WithSummary("Nộp báo cáo cuối kỳ hoặc phiên bản chỉnh sửa trước khi chấm")
            .WithDescription("""
                Gửi `multipart/form-data` gồm `groupId` và một tệp `file`. Chỉ sinh viên đã xác nhận
                tham gia nhóm được nộp. Nhóm phải được duyệt giữa kỳ, đang `InProgress` khi nộp lần đầu,
                hoặc `FinalReportSubmitted` khi nộp lại; đợt KLTN
                đang `Open` và trong cửa sổ `FINAL_EVALUATION` theo lịch và múi giờ của đợt.
                Thời điểm đúng deadline vẫn được chấp nhận.

                Chấp nhận PDF, DOC hoặc DOCX tối đa 20 MiB, kiểm tra cả phần mở rộng và nội dung.
                Thành công trả 200 với báo cáo loại `InitialFinal`, trạng thái `Submitted` và metadata file.
                Lần đầu tạo phiên bản 1 và chuyển nhóm sang `FinalReportSubmitted`. Nộp lại trong hạn
                tăng số phiên bản, giữ nguyên mã báo cáo, loại báo cáo và mọi file/audit cũ.
                Chỉ được nộp lại trước khi bắt đầu chấm (`Submitted`); `UnderEvaluation` trả 409.
                Các request dùng phiên bản dữ liệu cũ bị từ chối với 409, không ghi đè âm thầm.

                Yêu cầu Bearer token. 400: form/tệp không hợp lệ; 401: chưa xác thực;
                403: không có quyền; 404: không tìm thấy nhóm/đợt; 409: ngoài hạn, sai trạng thái,
                chưa duyệt giữa kỳ, đã bắt đầu chấm hoặc xung đột dữ liệu; 413: vượt giới hạn kích thước;
                415: Content-Type không phải multipart/form-data.
                """)
            .Produces<ApiResponse<FinalReportSubmissionDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces<ApiResponse<object>>(StatusCodes.Status413PayloadTooLarge)
            .Produces(StatusCodes.Status415UnsupportedMediaType);
        endpoints.MapGet("/api/thesis/groups/{groupId:guid}/final-report", GetHistoryAsync)
            .WithTags("ThesisManagement.FinalReport")
            .RequireAuthorization()
            .WithSummary("Xem lịch sử báo cáo cuối kỳ của nhóm")
            .WithDescription("""
                Trả báo cáo InitialFinal, trạng thái hiện tại và các phiên bản file theo số tăng dần.
                Chỉ Student đã Confirmed trong nhóm, GVHD hiện được phân công cho đề tài,
                Faculty có quyền thesis.review-gates.review hoặc Admin được xem.
                Vẫn được xem sau deadline, khi nhóm dừng hoặc báo cáo đã bắt đầu chấm.
                Metadata không chứa object key hoặc URL tải; lấy URL bằng endpoint tải file riêng.
                400: groupId rỗng; 401: chưa đăng nhập; 403: không có quyền; 404: không có nhóm/báo cáo.
                """)
            .Produces<ApiResponse<FinalReportHistoryDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/thesis/final-report-files/{fileId:guid}/download", DownloadAsync)
            .WithTags("ThesisManagement.FinalReport")
            .RequireAuthorization()
            .WithSummary("Lấy URL tải tạm thời cho một phiên bản báo cáo cuối kỳ")
            .WithDescription("""
                Kiểm tra file thuộc báo cáo cuối kỳ và quyền trên đúng nhóm trước khi cấp presigned URL 5 phút.
                Quyền tải giống quyền xem lịch sử; được tải cả phiên bản cũ sau deadline hoặc khi đã bắt đầu chấm.
                Bucket luôn private. Response chỉ gồm fileId, downloadUrl và expiresAtUtc; không có trường objectKey.
                400: fileId rỗng; 401: chưa đăng nhập; 403: không có quyền; 404: không có file hoặc sai loại file.
                """)
            .Produces<ApiResponse<SubmissionFileDownloadDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/thesis/final-reports/{finalReportId:guid}/begin-evaluation", BeginEvaluationAsync)
            .WithTags("ThesisManagement.FinalReport")
            .RequireAuthorization()
            .Accepts<BeginFinalReportEvaluationRequest>("application/json")
            .WithSummary("GVHD bắt đầu chấm và khóa phiên bản báo cáo cuối kỳ")
            .WithDescription("""
                Body JSON gồm expectedVersionNo (số nguyên dương) của phiên bản GVHD đang xem.
                Chỉ GVHD hiện được phân công cho đề tài hoặc Admin được bắt đầu chấm báo cáo InitialFinal
                đang Submitted; nhóm phải đang FinalReportSubmitted. Không bị giới hạn bởi deadline nộp.
                Nếu bản hiện tại đã thay đổi, trả 409 để tải lại dữ liệu trước khi bắt đầu chấm.
                Thành công chuyển báo cáo sang UnderEvaluation, khóa số phiên bản hiện tại và ghi audit cùng transaction.
                Không tăng phiên bản, sửa file hoặc đổi trạng thái nhóm. Request lặp lại trả 409, không tạo audit trùng.
                400: body/ID không hợp lệ; 401: chưa đăng nhập; 403: không phải GVHD hiện tại/Admin;
                404: không có báo cáo/nhóm/file hiện tại; 409: sai trạng thái hoặc xung đột dữ liệu; 415: sai Content-Type.
                """)
            .Produces<ApiResponse<FinalReportEvaluationStartedDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status415UnsupportedMediaType);
        return endpoints;
    }

    private static async Task<IResult> SubmitAsync(
        HttpRequest request,
        SubmitFinalReportHandler handler,
        CancellationToken cancellationToken)
    {
        // Read explicitly so malformed forms and Kestrel body-limit failures retain their HTTP status
        // instead of reaching the global exception middleware as unhandled binding exceptions.
        if (request.ContentLength > MaximumRequestSizeBytes)
        {
            return PayloadTooLarge();
        }

        var bodyLimit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false })
        {
            bodyLimit.MaxRequestBodySize = Math.Min(bodyLimit.MaxRequestBodySize ?? MaximumRequestSizeBytes, MaximumRequestSizeBytes);
        }

        if (!request.HasFormContentType
            || !request.ContentType!.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return PayloadTooLarge();
        }
        catch (BadHttpRequestException)
        {
            return InvalidRequest();
        }
        catch (InvalidDataException)
        {
            return InvalidRequest();
        }
        catch (IOException)
        {
            return InvalidRequest();
        }

        if (form["groupId"].Count != 1 || !Guid.TryParse(form["groupId"], out var groupId))
        {
            return InvalidRequest();
        }

        var file = form.Files.GetFile("file");
        if (file is null || form.Files.Count != 1 || file.Length == 0)
        {
            return Results.BadRequest(ApiResponse<FinalReportSubmissionDto>.Fail(ApiError.Create(
                FinalReportSubmissionErrors.InvalidFile.Code, FinalReportSubmissionErrors.InvalidFile.Message)));
        }

        if (file.Length > ThesisSubmissionFileValidator.MaximumFileSizeBytes)
        {
            return PayloadTooLarge();
        }

        await using var stream = file.OpenReadStream();
        return (await handler.HandleAsync(new SubmitFinalReportCommand(groupId, file.FileName, stream), cancellationToken))
            .ToHttpResult();
    }

    private static async Task<IResult> BeginEvaluationAsync(Guid finalReportId, HttpRequest request,
        BeginFinalReportEvaluationHandler handler, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType()) return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        BeginFinalReportEvaluationRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<BeginFinalReportEvaluationRequest>(cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return InvalidRequest();
        }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge) : InvalidRequest();
        }
        catch (IOException)
        {
            return InvalidRequest();
        }
        return (await handler.HandleAsync(new BeginFinalReportEvaluationCommand(finalReportId, body?.ExpectedVersionNo ?? 0),
            cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> GetHistoryAsync(Guid groupId, GetFinalReportHistoryHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(new GetFinalReportHistoryQuery(groupId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> DownloadAsync(Guid fileId, GetFinalReportFileDownloadHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(new GetFinalReportFileDownloadQuery(fileId), cancellationToken)).ToHttpResult();

    private static IResult InvalidRequest()
        => Results.BadRequest(ApiResponse<FinalReportSubmissionDto>.Fail(ApiError.Create(
            FinalReportSubmissionErrors.InvalidRequest.Code, FinalReportSubmissionErrors.InvalidRequest.Message)));

    private static IResult PayloadTooLarge()
        => Results.Json(ApiResponse<FinalReportSubmissionDto>.Fail(ApiError.Create(
            "FinalReport.FileTooLarge", "Tệp không được vượt quá 20 MiB; multipart request được cộng tối đa 64 KiB overhead.")),
            statusCode: StatusCodes.Status413PayloadTooLarge);

    public sealed record SubmitFinalReportRequest(Guid GroupId, IFormFile File);
    public sealed record BeginFinalReportEvaluationRequest(int ExpectedVersionNo);
}
