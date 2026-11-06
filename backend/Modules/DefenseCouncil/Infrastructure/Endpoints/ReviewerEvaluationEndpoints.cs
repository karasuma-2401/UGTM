using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

public static class ReviewerEvaluationEndpoints
{
    public static IEndpointRouteBuilder MapReviewerEvaluationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/thesis/reviewer-evaluations", SaveAsync)
            .WithTags("DefenseCouncil.ReviewerEvaluation")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                ReviewerEvaluationAuthorization.CanWrite(Roles(context.User))))
            .WithSummary("Lưu nháp hoặc chốt phiếu đánh giá của Giảng viên phản biện (GVPB)")
            .WithDescription("""
                Cho phép Giảng viên phản biện (GVPB) lưu nháp một phần tiêu chí hoặc chính thức chốt phiếu đánh giá báo cáo khóa luận tốt nghiệp theo barem tiêu chí của đợt KLTN.
                
                ### Headers bắt buộc:
                - `Idempotency-Key`: UUID định danh duy nhất cho request (bắt buộc mọi môi trường).
                  - Gửi lại cùng key và cùng ý định/nội dung sẽ trả lại đúng response và HTTP status code gốc (201 Created hoặc 200 OK).
                  - Dùng lại key với nội dung khác sẽ trả về lỗi 409 Conflict (`ReviewerEvaluation.IdempotencyConflict`).
                
                ### Tham số Request Body:
                - `assignmentId`: GUID định danh phân công phản biện (`ReviewerAssignment`).
                - `action`: Hành động thực hiện, nhận giá trị:
                  - `SaveDraft`: Lưu nháp phiếu đánh giá. Cho phép lưu một phần tiêu chí barem (`details`), nhận xét hoặc quyết định chưa hoàn chỉnh.
                  - `Submit`: Chốt phiếu đánh giá. Bắt buộc phải đánh giá đầy đủ tất cả các tiêu chí trong đợt KLTN, nhập nhận xét công khai và chọn quyết định đồng ý/không đồng ý bảo vệ.
                - `expectedAssignmentVersion`: Phiên bản hiện tại của phân công phản biện (bảo vệ chống ghi đè tương tranh).
                - `expectedEvaluationVersion`: Phiên bản phiếu đánh giá hiện tại khi sửa nháp (không gửi ở lần tạo nháp đầu tiên).
                - `details`: Danh sách điểm chi tiết theo tiêu chí barem đánh giá (`IReadOnlyList<CriterionScoreInputDto>?`):
                  - `criteriaId`: GUID định danh tiêu chí trong đợt KLTN (`GradingCriteria`).
                  - `score`: Điểm số đạt được của tiêu chí (số thực, `0 <= score <= criteria.MaxScore`).
                - `score`: Điểm tổng kết trực tiếp (tùy chọn, áp dụng khi không gửi `details` để tương thích ngược). Nếu gửi `details`, hệ thống tự động tính tổng điểm: `totalScore = Math.Round(Sum(score), 2)`.
                - `comment`: Nhận xét, đánh giá chuyên môn công khai cho sinh viên sau khi chốt (tối đa 2.000 ký tự Unicode / Runes). Bắt buộc khi `action = Submit`.
                - `defenseApproval`: Quyết định phê duyệt bảo vệ (`Approved` hoặc `Rejected`). Bắt buộc khi `action = Submit`.
                
                ### Quy tắc nghiệp vụ & Barem tiêu chí:
                1. **Barem tiêu chí đánh giá:**
                   - Tự động lấy danh sách tiêu chí của đợt KLTN (`BatchId`) tương ứng với phân công phản biện.
                   - Kiểm tra tính hợp lệ:
                     - Không được trùng lặp tiêu chí trong cùng một request (`ReviewerEvaluation.DuplicateCriteria`).
                     - Tiêu chí phải thuộc đúng đợt KLTN (`ReviewerEvaluation.CriteriaNotFound`).
                     - Điểm tiêu chí không âm và không vượt trần (`ReviewerEvaluation.CriteriaScoreNegative`, `ReviewerEvaluation.ScoreExceedsMaxScore`).
                     - Phân biệt theo hành động: Khi `SaveDraft`, cho phép lưu nháp một phần tiêu chí barem. Khi `Submit`, bắt buộc phải chấm đủ 100% tất cả các tiêu chí trong đợt (`ReviewerEvaluation.MissingRequiredCriteria`).
                2. **Tự động tính tổng điểm và lưu chi tiết:**
                   - Tổng điểm tự động tính: `totalScore = Math.Round(Sum(score), 2)` và lưu vào cột `Score` của phiếu phản biện.
                   - Lưu toàn bộ chi tiết từng tiêu chí vào bảng `councils.evaluation_detail`.
                3. **Tương thích ngược (Backward Compatibility):**
                   - Cho phép gửi `score` trực tiếp nếu không gửi mảng `details`.
                4. **Khóa phiếu phản biện:**
                   - Khi đã `Submit`, phiếu chuyển sang trạng thái đã chốt (`Submitted`) và bị khóa vĩnh viễn (khóa cả trường hợp `Approved` lẫn `Rejected`).
                   - Không thể chỉnh sửa phiếu đã chốt (`ReviewerEvaluation.EvaluationLocked`).
                
                ### Phân quyền truy cập (RBAC):
                Chỉ Giảng viên phản biện (`Lecturer`) đang hoạt động (`Active`) và được phân công cho đề tài này, hoặc Quản trị viên (`Admin`).
                
                ### Mã lỗi và phản hồi:
                - **201 Created:** Tạo mới phiếu phản biện thành công (lần đầu).
                - **200 OK:** Cập nhật hoặc chốt phiếu phản biện thành công.
                - **400 Bad Request:**
                  - `ReviewerEvaluation.CriteriaRequired`: Mảng tiêu chí bị rỗng khi gửi `details`.
                  - `ReviewerEvaluation.DuplicateCriteria`: Tiêu chí bị trùng lặp trong mảng `details`.
                  - `ReviewerEvaluation.CriteriaNotFound`: Tiêu chí không tồn tại trong đợt KLTN.
                  - `ReviewerEvaluation.CriteriaScoreNegative`: Điểm tiêu chí nhỏ hơn 0.
                  - `ReviewerEvaluation.ScoreExceedsMaxScore`: Điểm tiêu chí vượt quá mức tối đa quy định.
                  - `ReviewerEvaluation.MissingRequiredCriteria`: Chưa chấm đủ tất cả các tiêu chí bắt buộc khi thực hiện chốt phiếu (`Submit`).
                  - `ReviewerEvaluation.InvalidInput`: Thiếu header Idempotency-Key hoặc dữ liệu JSON không hợp lệ.
                - **401 Unauthorized:** Chưa đăng nhập hoặc Bearer token không hợp lệ.
                - **403 Forbidden:** Người dùng không phải là GVPB được phân công hoặc không đủ quyền.
                - **404 Not Found:**
                  - `ReviewerEvaluation.AssignmentNotFound`: Không tìm thấy phân công phản biện tương ứng.
                - **409 Conflict:**
                  - `ReviewerEvaluation.EvaluationLocked`: Phiếu phản biện đã được chốt, không thể chỉnh sửa.
                  - `ReviewerEvaluation.AssignmentVersionConflict`: Xung đột phiên bản phân công phản biện.
                  - `ReviewerEvaluation.ConcurrencyConflict`: Xung đột phiên bản phiếu đánh giá giữa các phiên ghi.
                  - `ReviewerEvaluation.IdempotencyConflict`: Sử dụng lại Idempotency-Key với nội dung khác.
                - **413 Payload Too Large:** Dung lượng request vượt quá giới hạn.
                - **415 Unsupported Media Type:** Request không phải định dạng `application/json`.
                """)
            .Produces<ApiResponse<ReviewerEvaluationDto>>(StatusCodes.Status201Created)
            .Produces<ApiResponse<ReviewerEvaluationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status413PayloadTooLarge)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .WithName("DefenseCouncil.ReviewerEvaluation.Save");

        endpoints.MapGet("/api/thesis/reviewer-assignments/{assignmentId:guid}/evaluation", GetAsync)
            .WithTags("DefenseCouncil.ReviewerEvaluation")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                ReviewerEvaluationAuthorization.CanRead(Roles(context.User), context.User.FindAll("permission").Select(claim => claim.Value))))
            .WithSummary("Xem phiếu đánh giá phản biện đầy đủ theo phân công")
            .WithDescription("""
                Tra cứu toàn bộ thông tin phiếu đánh giá phản biện của nhóm đề tài theo mã phân công phản biện, bao gồm kết quả đánh giá, nhận xét và chi tiết barem điểm theo tiêu chí.
                
                ### Tham số URL:
                - `assignmentId`: GUID định danh phân công phản biện (`ReviewerAssignment`).
                
                ### Cấu trúc dữ liệu phản hồi (`ReviewerEvaluationDto`):
                - `id`: GUID định danh phiếu đánh giá phản biện.
                - `assignmentId`: GUID phân công phản biện.
                - `topicId`: GUID đề tài khóa luận.
                - `batchId`: GUID đợt KLTN.
                - `score`: Tổng điểm đánh giá phản biện (làm tròn 2 chữ số thập phân).
                - `comment`: Nhận xét chuyên môn của GVPB dành cho sinh viên.
                - `defenseApproval`: Quyết định đồng ý cho bảo vệ (`Approved` hoặc `Rejected`).
                - `status`: Trạng thái phiếu phản biện (`Draft` / `Submitted`).
                - `isLocked`: Cờ boolean cho biết phiếu đã bị khóa hay chưa.
                - `evaluationVersion`: Phiên bản phiếu đánh giá dùng cho kiểm soát tương tranh lạc quan khi cập nhật.
                - `evaluatedAt`: Thời điểm đánh giá / chốt phiếu.
                - `details`: Danh sách điểm chi tiết từng tiêu chí theo barem của đợt KLTN (`IReadOnlyList<EvaluationDetailResponseDto>`):
                  - `id`: GUID dòng chi tiết điểm (`councils.evaluation_detail`).
                  - `criteriaId`: GUID tiêu chí đánh giá.
                  - `criteriaCode`: Mã tiêu chí (ví dụ: 'ND01', 'BC01').
                  - `criteriaName`: Tên tiêu chí đánh giá.
                  - `score`: Điểm đạt được của tiêu chí.
                  - `maxScore`: Điểm tối đa của tiêu chí.
                
                ### Phân quyền truy cập (RBAC):
                - Giảng viên phản biện (`Lecturer`) được phân công đề tài này (đang hoạt động `Active`).
                - Quản trị viên hệ thống (`Admin`).
                - Cán bộ Văn phòng Khoa (`Faculty`) hoặc Chuyên viên Phòng Đào tạo (`AcademicAffairs`) có quyền `thesis.reviewer-evaluations.read`.
                
                ### Mã lỗi và phản hồi:
                - **200 OK:** Tra cứu thành công, trả về `ApiResponse<ReviewerEvaluationDto>`.
                - **400 Bad Request:** Request có query parameter không hợp lệ.
                - **401 Unauthorized:** Chưa đăng nhập hoặc Bearer token không hợp lệ.
                - **403 Forbidden:** Người dùng không có quyền xem phiếu phản biện này.
                - **404 Not Found:**
                  - `ReviewerEvaluation.NotFound`: Phân công chưa có phiếu đánh giá phản biện nào.
                - **409 Conflict:**
                  - `ReviewerEvaluation.LegacyEvaluationConflict`: Phân công thuộc dữ liệu cũ (legacy) không hỗ trợ tra cứu.
                """)
            .Produces<ApiResponse<ReviewerEvaluationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.ReviewerEvaluation.GetByAssignment");
        return endpoints;
    }

    private static IEnumerable<string> Roles(ClaimsPrincipal principal)
        => principal.FindAll(ClaimTypes.Role).Concat(principal.FindAll("role")).Select(claim => claim.Value);

    private static async Task<IResult> GetAsync(Guid assignmentId, HttpRequest request, GetReviewerEvaluationHandler handler, CancellationToken token)
        => request.Query.Count != 0 ? InvalidRequest() : (await handler.HandleAsync(assignmentId, token)).ToHttpResult();

    private static async Task<IResult> SaveAsync(HttpRequest request, SaveReviewerEvaluationHandler handler, CancellationToken token)
    {
        if (!request.HasJsonContentType()) return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (request.Query.Count != 0 || !request.Headers.TryGetValue("Idempotency-Key", out var keys) || keys.Count != 1
            || !Guid.TryParse(keys[0], out var key) || key == Guid.Empty) return InvalidRequest();
        SaveReviewerEvaluationCommand? command;
        try
        {
            using var json = await JsonDocument.ParseAsync(request.Body, new JsonDocumentOptions { MaxDepth = 16 }, token);
            command = Parse(json.RootElement, key);
        }
        catch (JsonException) { return InvalidRequest(); }
        catch (InvalidOperationException) { return InvalidRequest(); }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge) : InvalidRequest();
        }
        catch (IOException) { return InvalidRequest(); }
        if (command is null) return InvalidRequest();
        var result = await handler.HandleAsync(command, token);
        return result.IsFailure ? result.ToHttpResult()
            : Results.Json(ApiResponse<ReviewerEvaluationDto>.Ok(result.Value.Evaluation), statusCode: result.Value.StatusCode);
    }

    private static SaveReviewerEvaluationCommand? Parse(JsonElement root, Guid key)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (!new[] { "assignmentId", "action", "expectedAssignmentVersion", "expectedEvaluationVersion", "score", "comment", "defenseApproval", "details" }
                    .Contains(property.Name, StringComparer.OrdinalIgnoreCase) || !fields.TryAdd(property.Name, property.Value)) return null;
        }
        if (!fields.TryGetValue("assignmentId", out var id) || id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var assignmentId)
            || !fields.TryGetValue("action", out var action) || !TryEnum<ReviewerEvaluationAction>(action, out var parsedAction)
            || !fields.TryGetValue("expectedAssignmentVersion", out var version) || version.ValueKind != JsonValueKind.Number
            || !version.TryGetUInt32(out var assignmentVersion)) return null;
        uint? evaluationVersion = null;
        if (fields.TryGetValue("expectedEvaluationVersion", out var ev) && ev.ValueKind != JsonValueKind.Null)
        {
            if (ev.ValueKind != JsonValueKind.Number || !ev.TryGetUInt32(out var value)) return null;
            evaluationVersion = value;
        }
        decimal? score = null;
        if (fields.TryGetValue("score", out var sc) && sc.ValueKind != JsonValueKind.Null)
        {
            if (sc.ValueKind != JsonValueKind.Number || !sc.TryGetDecimal(out var value)) return null;
            score = value;
        }
        string? comment = null;
        if (fields.TryGetValue("comment", out var co) && co.ValueKind != JsonValueKind.Null)
        {
            if (co.ValueKind != JsonValueKind.String) return null;
            comment = co.GetString();
        }
        DefenseApprovalStatus? decision = null;
        if (fields.TryGetValue("defenseApproval", out var de) && de.ValueKind != JsonValueKind.Null)
        {
            if (!TryEnum<DefenseApprovalStatus>(de, out var value)) return null;
            decision = value;
        }

        List<CriterionScoreInputDto>? details = null;
        if (fields.TryGetValue("details", out var det) && det.ValueKind != JsonValueKind.Null)
        {
            if (det.ValueKind != JsonValueKind.Array) return null;
            details = [];
            foreach (var item in det.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) return null;
                Guid critId = Guid.Empty;
                decimal? critScore = null;
                foreach (var prop in item.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "criteriaId", StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.Value.ValueKind != JsonValueKind.String || !prop.Value.TryGetGuid(out critId) || critId == Guid.Empty) return null;
                    }
                    else if (string.Equals(prop.Name, "score", StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetDecimal(out var s)) return null;
                        critScore = s;
                    }
                    else return null;
                }
                if (critId == Guid.Empty || !critScore.HasValue) return null;
                details.Add(new CriterionScoreInputDto(critId, critScore.Value));
            }
        }

        return new(assignmentId, parsedAction, assignmentVersion, evaluationVersion, score, comment, decision, key, details);
    }

    private static bool TryEnum<T>(JsonElement value, out T parsed) where T : struct, Enum
    {
        parsed = default;
        return value.ValueKind == JsonValueKind.String && Enum.GetNames<T>().Contains(value.GetString()?.Trim(), StringComparer.OrdinalIgnoreCase)
            && Enum.TryParse(value.GetString()!.Trim(), true, out parsed);
    }
    private static IResult InvalidRequest() => Results.BadRequest(ApiResponse<object>.Fail(
        ApiError.Create(ReviewerEvaluationErrors.InvalidRequest.Code, ReviewerEvaluationErrors.InvalidRequest.Message)));

}
