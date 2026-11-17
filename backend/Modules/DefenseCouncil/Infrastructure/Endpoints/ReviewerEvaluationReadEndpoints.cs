using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

public static class ReviewerEvaluationReadEndpoints
{
    public static IEndpointRouteBuilder MapReviewerEvaluationReadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/thesis/groups/{groupId:guid}/feedback", FeedbackAsync)
            .WithTags("DefenseCouncil.ReviewerEvaluation")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireAssertion(context => Roles(context.User).Contains("Student", StringComparer.OrdinalIgnoreCase)))
            .WithSummary("Sinh viên xem góp ý phản biện đã chốt của nhóm mình")
            .WithDescription("""
                Cần Student và membership Confirmed đúng nhóm; không có Admin override.
                DTO chỉ có comment, null khi chưa có góp ý đã phát hành; không trả điểm, kết luận hoặc gate.
                Góp ý gắn đúng snapshot đã phát hành, không tự mất khi đóng đợt/GVPB nghỉ/nhóm chuyển giai đoạn.
                Không nhận query selector; không fallback nháp/legacy/RevisionFeedback. Đọc không tạo audit hoặc đổi trạng thái.
                """)
            .Produces<ApiResponse<ReviewerFeedbackDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden).Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);
        endpoints.MapGet("/api/thesis/groups/{groupId:guid}/defense-eligibility", EligibilityAsync)
            .WithTags("DefenseCouncil.ReviewerEvaluation")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                ReviewerEvaluationAuthorization.CanRead(Roles(context.User), context.User.FindAll("permission").Select(claim => claim.Value))))
            .WithSummary("Xem điều kiện chuyên môn ra Hội đồng theo cấu hình Active hiện tại")
            .WithDescription("""
                Admin, Faculty/PĐT có read permission hoặc own reviewer Active/Lecturer đọc theo quyền.
                Thiếu điều kiện trả 200 canProceed=false với reasonCode; không trả raw score/comment hoặc phiếu GVHD.
                Cấu hình thiếu/mâu thuẫn không mặc định 5.0. BatchStatus chỉ là thông tin; GET không phải proof xếp Hội đồng.
                Scheduling writer phải dùng LockForSchedulingAsync trong coordinator transaction của chính lần ghi.
                Không nhận query; không đổi trạng thái/audit hoặc mở lại nhóm đã bảo vệ.
                """)
            .Produces<ApiResponse<ReviewerDefenseEligibilityDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden).Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);
        return endpoints;
    }
    private static IEnumerable<string> Roles(ClaimsPrincipal principal)
        => principal.FindAll(ClaimTypes.Role).Concat(principal.FindAll("role")).Select(claim => claim.Value);
    private static IResult Invalid() => Results.BadRequest(ApiResponse<object>.Fail(ApiError.Create(ReviewerEvaluationErrors.InvalidRequest.Code, ReviewerEvaluationErrors.InvalidRequest.Message)));
    private static async Task<IResult> FeedbackAsync(Guid groupId, HttpRequest request, GetReviewerFeedbackHandler handler, CancellationToken token)
        => request.Query.Count != 0 ? Invalid() : (await handler.HandleAsync(groupId, token)).ToHttpResult();
    private static async Task<IResult> EligibilityAsync(Guid groupId, HttpRequest request, GetReviewerDefenseEligibilityHandler handler, CancellationToken token)
        => request.Query.Count != 0 ? Invalid() : (await handler.HandleAsync(groupId, token)).ToHttpResult();
}
