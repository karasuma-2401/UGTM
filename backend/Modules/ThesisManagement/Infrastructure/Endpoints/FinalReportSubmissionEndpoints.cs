using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.SubmitFinalReport;
using UGTM.Modules.ThesisManagement.Application.Storage;

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

    private static IResult InvalidRequest()
        => Results.BadRequest(ApiResponse<FinalReportSubmissionDto>.Fail(ApiError.Create(
            FinalReportSubmissionErrors.InvalidRequest.Code, FinalReportSubmissionErrors.InvalidRequest.Message)));

    private static IResult PayloadTooLarge()
        => Results.Json(ApiResponse<FinalReportSubmissionDto>.Fail(ApiError.Create(
            "FinalReport.FileTooLarge", "Tệp không được vượt quá 20 MiB; multipart request được cộng tối đa 64 KiB overhead.")),
            statusCode: StatusCodes.Status413PayloadTooLarge);

    public sealed record SubmitFinalReportRequest(Guid GroupId, IFormFile File);
}
