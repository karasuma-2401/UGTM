using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Application.ProgressMilestones.CreateProgressMilestone;
using UGTM.Modules.ThesisManagement.Application.ProgressMilestones.Dtos;
using UGTM.Modules.ThesisManagement.Application.ProgressMilestones.GetProgressMilestones;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class ProgressMilestoneEndpoints
{
    public static IEndpointRouteBuilder MapProgressMilestoneEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/thesis/groups/{groupId:guid}/progress-milestones")
            .WithTags("ThesisManagement.ProgressMilestones")
            .RequireAuthorization();

        group.MapPost("", CreateAsync)
            .WithSummary("GVHD tạo mốc công việc tiến độ cho nhóm khóa luận")
            .WithDescription("""
                GVHD được phân công cho đề tài hoặc `Admin` tạo các mốc công việc (Milestone) cho một nhóm.
                Mỗi mốc gồm `title`, `description` tùy chọn và `deadlineUtc` bắt buộc ở tương lai theo UTC.
                Hệ thống chỉ cho tạo sau khi đề cương chi tiết của nhóm đã được Khoa duyệt tại Cổng 1, tức
                nhóm đang ở trạng thái `InProgress`.

                Quyền GVHD được kiểm tra động bằng `TopicId` của nhóm và người hướng dẫn hiện được gán cho đề tài,
                không dựa vào role `Faculty` chung. Mỗi lần tạo thành công đều có audit log.

                Yêu cầu Bearer token của đúng GVHD hoặc `Admin`.
                - 200 OK: tạo milestone thành công.
                - 400 Bad Request: title hoặc deadline UTC không hợp lệ.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không phải GVHD được gán hoặc Admin.
                - 404 Not Found: không tìm thấy nhóm.
                - 409 Conflict: nhóm chưa qua Cổng 1 hoặc có xung đột đồng thời.
                """)
            .Produces<ApiResponse<ProgressMilestoneDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapGet("", GetAsync)
            .WithSummary("GVHD xem danh sách mốc tiến độ của nhóm khóa luận")
            .WithDescription("""
                Trả danh sách milestone của nhóm theo deadline tăng dần. Chỉ GVHD hiện được phân công cho đề tài
                hoặc `Admin` có thể xem. Endpoint được giữ riêng cho luồng làm việc của GVHD; sinh viên sẽ nhận
                API nộp báo cáo theo từng milestone ở checkpoint tiếp theo.

                Yêu cầu Bearer token của đúng GVHD hoặc `Admin`.
                - 200 OK: danh sách milestone, có thể rỗng nếu GVHD chưa tạo mốc nào.
                - 400 Bad Request: groupId không hợp lệ.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không phải GVHD được gán hoặc Admin.
                - 404 Not Found: không tìm thấy nhóm.
                """)
            .Produces<ApiResponse<IReadOnlyList<ProgressMilestoneDto>>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        Guid groupId,
        [FromBody] CreateProgressMilestoneRequest? request,
        CreateProgressMilestoneHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new CreateProgressMilestoneCommand(
                groupId,
                request?.Title,
                request?.Description,
                request?.DeadlineUtc ?? default),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetAsync(
        Guid groupId,
        GetProgressMilestonesHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new GetProgressMilestonesQuery(groupId),
            cancellationToken)).ToHttpResult();

    public sealed record CreateProgressMilestoneRequest(
        string? Title,
        string? Description,
        DateTime? DeadlineUtc);
}
