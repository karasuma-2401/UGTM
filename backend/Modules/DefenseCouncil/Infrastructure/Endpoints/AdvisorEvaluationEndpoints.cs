using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.DecideAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.GetAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

public static class AdvisorEvaluationEndpoints
{
    public static IEndpointRouteBuilder MapAdvisorEvaluationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/thesis/advisor-evaluations", SaveAsync)
            .WithTags("DefenseCouncil.AdvisorEvaluation")
            .RequireAuthorization()
            .Accepts<SaveAdvisorEvaluationRequest>("application/json")
            .WithSummary("Tạo hoặc cập nhật phiếu chấm điểm của Giảng viên hướng dẫn (GVHD)")
            .WithDescription("""
                Cho phép Giảng viên hướng dẫn (GVHD) chấm điểm và đánh giá báo cáo khóa luận tốt nghiệp cuối kỳ (`InitialFinal`) của nhóm sinh viên theo barem tiêu chí chi tiết của đợt KLTN.
                
                ### Headers bắt buộc:
                - `Idempotency-Key`: UUID định danh duy nhất cho request (bắt buộc mọi môi trường).
                  - Gửi lại cùng key và cùng nội dung sẽ trả lại đúng response đã lưu (idempotent retry).
                  - Dùng lại key với nội dung thay đổi sẽ trả về lỗi 409 Conflict (`AdvisorEvaluation.IdempotencyConflict`).
                
                ### Tham số Request Body (`SaveAdvisorEvaluationRequest`):
                - `groupId`: GUID định danh nhóm đề tài cần chấm điểm (`StudentGroup`).
                - `expectedReportVersionNo`: Số thứ tự phiên bản báo cáo cuối kỳ dự kiến (`InitialFinal` version, số nguyên dương).
                - `expectedEvaluationVersion`: Phiên bản phiếu đánh giá hiện tại khi cập nhật (bắt buộc khi sửa, bỏ trống ở lần chấm đầu tiên).
                - `details`: Danh sách điểm chi tiết theo tiêu chí barem đánh giá (`IReadOnlyList<CriterionScoreInputDto>?`):
                  - `criteriaId`: GUID định danh tiêu chí đánh giá trong đợt KLTN (`GradingCriteria`).
                  - `score`: Điểm số đạt được của tiêu chí (số thực, `0 <= score <= criteria.MaxScore`).
                - `score`: Điểm tổng kết trực tiếp (tùy chọn, áp dụng khi không gửi `details` để tương thích ngược). Nếu gửi `details`, hệ thống tự động tính tổng điểm: `totalScore = Math.Round(Sum(score), 2)`.
                - `comment`: Nhận xét, đánh giá chuyên môn của GVHD dành cho nhóm sinh viên (tối đa 2000 ký tự).
                
                ### Quy tắc nghiệp vụ & Barem tiêu chí:
                1. **Barem tiêu chí đánh giá:**
                   - Hệ thống tự động xác định đợt KLTN (`BatchId`) của nhóm đề tài và tải toàn bộ tiêu chí đánh giá tương ứng.
                   - Kiểm tra tính hợp lệ của tiêu chí:
                     - Không được trùng lặp tiêu chí trong cùng một request (`AdvisorEvaluation.DuplicateCriteria`).
                     - Tiêu chí phải thuộc đúng đợt KLTN của đề tài (`AdvisorEvaluation.CriteriaNotFound`).
                     - Điểm tiêu chí không được nhỏ hơn 0 (`AdvisorEvaluation.CriteriaScoreNegative`) và không vượt quá điểm tối đa cho phép (`AdvisorEvaluation.ScoreExceedsMaxScore`).
                     - Bắt buộc phải chấm đủ tất cả các tiêu chí trong barem của đợt (`AdvisorEvaluation.MissingRequiredCriteria`).
                2. **Tự động tính tổng điểm và lưu chi tiết:**
                   - Tổng điểm được tự động tính: `totalScore = Math.Round(Sum(score), 2)` và lưu vào cột `Score` của phiếu đánh giá.
                   - Toàn bộ chi tiết từng tiêu chí được lưu vào bảng `councils.evaluation_detail`.
                3. **Tương thích ngược (Backward Compatibility):**
                   - Nếu client không gửi `details`, hệ thống cho phép dùng trường `score` trực tiếp theo cơ chế cũ.
                4. **Khóa phiếu & Kiểm soát tương tranh:**
                   - Phiếu đánh giá đã được duyệt chuyển tiếp sang phản biện sẽ bị khóa hoàn toàn, không thể chỉnh sửa (`AdvisorEvaluation.EvaluationLocked`).
                   - Kiểm tra `expectedReportVersionNo` và `expectedEvaluationVersion` để ngăn chặn ghi đè tương tranh giữa nhiều phiên làm việc.
                
                ### Phân quyền truy cập (RBAC):
                Chỉ Giảng viên hướng dẫn (`Lecturer`) đang được phân công hướng dẫn nhóm đề tài này, hoặc Quản trị viên hệ thống (`Admin`).
                
                ### Mã lỗi và phản hồi:
                - **200 OK:** Lưu phiếu thành công, trả về `ApiResponse<AdvisorEvaluationDto>` kèm tổng điểm và danh sách chi tiết barem.
                - **400 Bad Request:**
                  - `AdvisorEvaluation.CriteriaRequired`: Mảng tiêu chí bị rỗng khi có gửi `details`.
                  - `AdvisorEvaluation.DuplicateCriteria`: Tiêu chí bị trùng lặp trong mảng `details`.
                  - `AdvisorEvaluation.CriteriaNotFound`: Tiêu chí không tồn tại trong đợt KLTN.
                  - `AdvisorEvaluation.CriteriaScoreNegative`: Điểm tiêu chí nhỏ hơn 0.
                  - `AdvisorEvaluation.ScoreExceedsMaxScore`: Điểm tiêu chí vượt quá mức tối đa quy định.
                  - `AdvisorEvaluation.MissingRequiredCriteria`: Chưa chấm đủ tất cả các tiêu chí bắt buộc trong đợt.
                  - `AdvisorEvaluation.InvalidInput`: Thiếu header Idempotency-Key hoặc dữ liệu JSON không hợp lệ.
                - **401 Unauthorized:** Chưa đăng nhập hoặc Bearer token không hợp lệ.
                - **403 Forbidden:**
                  - `AdvisorEvaluation.Forbidden`: Người dùng không phải là GVHD của nhóm đề tài này.
                - **404 Not Found:**
                  - `AdvisorEvaluation.ReportNotFound`: Không tìm thấy báo cáo cuối kỳ tương ứng của nhóm.
                - **409 Conflict:**
                  - `AdvisorEvaluation.EvaluationLocked`: Phiếu chấm đã được phê duyệt, không thể chỉnh sửa.
                  - `AdvisorEvaluation.ReportVersionMismatch`: Phiên bản báo cáo không khớp với thực tế.
                  - `AdvisorEvaluation.EvaluationVersionMismatch`: Phiên bản phiếu đánh giá bị xung đột tương tranh.
                  - `AdvisorEvaluation.IdempotencyConflict`: Sử dụng lại Idempotency-Key với nội dung khác.
                - **415 Unsupported Media Type:** Request không phải định dạng `application/json`.
                """)
            .Produces<ApiResponse<AdvisorEvaluationDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .WithName("DefenseCouncil.AdvisorEvaluation.Save");

        endpoints.MapGet("/api/thesis/groups/{groupId:guid}/advisor-evaluation", GetAsync)
            .WithTags("DefenseCouncil.AdvisorEvaluation")
            .RequireAuthorization()
            .WithSummary("Xem phiếu chấm điểm của Giảng viên hướng dẫn (GVHD) theo nhóm đề tài")
            .WithDescription("""
                Tra cứu toàn bộ thông tin phiếu chấm điểm của Giảng viên hướng dẫn cho nhóm đề tài khóa luận tốt nghiệp, bao gồm điểm tổng kết, nhận xét và chi tiết barem điểm theo tiêu chí.
                
                ### Tham số URL:
                - `groupId`: GUID định danh nhóm đề tài khóa luận tốt nghiệp (`StudentGroup`).
                
                ### Cấu trúc dữ liệu phản hồi (`AdvisorEvaluationDto`):
                - `id`: GUID định danh phiếu chấm điểm của GVHD.
                - `finalReportId`: GUID báo cáo khóa luận cuối kỳ.
                - `score`: Tổng điểm đánh giá của GVHD (làm tròn 2 chữ số thập phân).
                - `comment`: Nhận xét, đánh giá của GVHD dành cho nhóm sinh viên.
                - `status`: Trạng thái phiếu chấm (`Draft` / `Submitted`).
                - `isLocked`: Cờ boolean xác định phiếu đã bị khóa hay chưa.
                - `evaluationVersion`: Phiên bản phiếu đánh giá dùng cho kiểm soát tương tranh lạc quan khi lưu hoặc quyết định chuyển tiếp.
                - `evaluatedAt`: Thời điểm chấm điểm.
                - `details`: Danh sách chi tiết điểm theo từng tiêu chí barem của đợt KLTN (`IReadOnlyList<EvaluationDetailResponseDto>`):
                  - `id`: GUID dòng chi tiết điểm (`councils.evaluation_detail`).
                  - `criteriaId`: GUID tiêu chí đánh giá.
                  - `criteriaCode`: Mã tiêu chí đánh giá.
                  - `criteriaName`: Tên tiêu chí đánh giá.
                  - `score`: Điểm số đạt được của tiêu chí.
                  - `maxScore`: Điểm tối đa cho phép của tiêu chí.
                
                ### Phân quyền truy cập (RBAC):
                Chỉ Giảng viên hướng dẫn (`Lecturer`) đang được phân công hướng dẫn đề tài này, hoặc Quản trị viên hệ thống (`Admin`). Đọc phiếu không tạo nhật ký kiểm toán và không làm thay đổi trạng thái đề tài/quy trình.
                
                ### Mã lỗi và phản hồi:
                - **200 OK:** Tra cứu thành công, trả về `ApiResponse<AdvisorEvaluationDto>`.
                - **400 Bad Request:** Request không hợp lệ.
                - **401 Unauthorized:** Chưa đăng nhập hoặc Bearer token không hợp lệ.
                - **403 Forbidden:**
                  - `AdvisorEvaluation.Forbidden`: Người dùng không phải là GVHD của nhóm đề tài này và không phải Admin.
                - **404 Not Found:**
                  - `AdvisorEvaluation.EvaluationNotFound`: Nhóm đề tài chưa có phiếu chấm điểm của GVHD.
                - **409 Conflict:** Báo cáo hoặc phiếu ở trạng thái xung đột không thể tra cứu.
                """)
            .Produces<ApiResponse<AdvisorEvaluationDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.AdvisorEvaluation.GetByGroup");

        endpoints.MapPut("/api/thesis/final-reports/{finalReportId:guid}/approval", DecideAsync)
            .WithTags("DefenseCouncil.AdvisorEvaluation")
            .RequireAuthorization()
            .Accepts<DecideAdvisorEvaluationRequest>("application/json")
            .WithSummary("Duyệt hoặc chưa đồng ý chuyển báo cáo sang phản biện")
            .WithDescription("""
                Chỉ GVHD hiện được phân công cho đề tài hoặc Admin có thể quyết định trên phiếu chấm
                chưa bị khóa của báo cáo InitialFinal đang UnderEvaluation. Body phải có
                `expectedReportVersionNo`, `expectedEvaluationVersion` và `allowsReviewerAssignment`.
                Khi chưa đồng ý, `rejectionReason` là bắt buộc. Khi đồng ý, không được gửi lý do từ
                chối; hệ thống khóa phiếu và chuyển nhóm sang ReadyForReviewerAssignment trong cùng
                transaction. Header Idempotency-Key là UUID bắt buộc: gửi lại cùng key và nội dung
                trả đúng response đã lưu, còn dùng lại key với nội dung khác trả 409.
                """)
            .Produces<ApiResponse<AdvisorEvaluationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .WithName("DefenseCouncil.AdvisorEvaluation.Decide");
        return endpoints;
    }

    private static async Task<IResult> SaveAsync(
        HttpRequest request,
        SaveAdvisorEvaluationHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        if (!TryGetIdempotencyKey(request, out var idempotencyKey))
        {
            return InvalidRequest();
        }

        SaveAdvisorEvaluationRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<SaveAdvisorEvaluationRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return InvalidRequest();
        }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge)
                : InvalidRequest();
        }
        catch (IOException)
        {
            return InvalidRequest();
        }

        return (await handler.HandleAsync(new SaveAdvisorEvaluationCommand(
            body?.FinalReportId ?? Guid.Empty,
            body?.ExpectedReportVersionNo ?? 0,
            body?.ExpectedEvaluationVersion,
            body?.Score,
            body?.Comment,
            idempotencyKey,
            body?.Details), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> GetAsync(
        Guid groupId,
        GetAdvisorEvaluationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(groupId, cancellationToken)).ToHttpResult();

    private static async Task<IResult> DecideAsync(
        Guid finalReportId,
        HttpRequest request,
        DecideAdvisorEvaluationHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        if (!TryGetIdempotencyKey(request, out var idempotencyKey))
        {
            return InvalidRequest();
        }

        DecideAdvisorEvaluationRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<DecideAdvisorEvaluationRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return InvalidRequest();
        }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge)
                : InvalidRequest();
        }
        catch (IOException)
        {
            return InvalidRequest();
        }

        return (await handler.HandleAsync(new DecideAdvisorEvaluationCommand(
            finalReportId,
            body?.ExpectedReportVersionNo ?? 0,
            body?.ExpectedEvaluationVersion,
            body?.AllowsReviewerAssignment,
            body?.RejectionReason,
            idempotencyKey), cancellationToken)).ToHttpResult();
    }

    private static bool TryGetIdempotencyKey(HttpRequest request, out Guid idempotencyKey)
    {
        idempotencyKey = Guid.Empty;
        if (request.Headers.TryGetValue("Idempotency-Key", out var values)
            && values.Count == 1
            && Guid.TryParse(values[0], out idempotencyKey)
            && idempotencyKey != Guid.Empty)
        {
            return true;
        }

        var env = request.HttpContext.RequestServices.GetService<IHostEnvironment>();
        if (env?.IsDevelopment() == true && (!request.Headers.ContainsKey("Idempotency-Key") || string.IsNullOrWhiteSpace(request.Headers["Idempotency-Key"])))
        {
            idempotencyKey = Guid.NewGuid();
            return true;
        }

        return false;
    }

    private static IResult InvalidRequest()
        => Results.BadRequest(ApiResponse<AdvisorEvaluationDto>.Fail(ApiError.Create(
            AdvisorEvaluationErrors.InvalidRequest.Code, AdvisorEvaluationErrors.InvalidRequest.Message)));

    public sealed record SaveAdvisorEvaluationRequest(
        Guid FinalReportId,
        int ExpectedReportVersionNo,
        uint? ExpectedEvaluationVersion,
        decimal? Score,
        string? Comment,
        List<CriterionScoreInputDto>? Details = null);

    public sealed record DecideAdvisorEvaluationRequest(
        int ExpectedReportVersionNo,
        uint? ExpectedEvaluationVersion,
        bool? AllowsReviewerAssignment,
        string? RejectionReason);
}
