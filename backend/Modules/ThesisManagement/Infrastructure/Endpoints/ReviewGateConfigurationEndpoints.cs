using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.Dtos;
using UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.GetReviewGateConfigurations;
using UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.UpsertReviewGateConfiguration;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class ReviewGateConfigurationEndpoints
{
    private const string ManagePolicy = "Permission:" + PermissionCodes.ThesisReviewGateConfigurationsManage;

    public static IEndpointRouteBuilder MapReviewGateConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/thesis/review-gate-configurations")
            .WithTags("ThesisManagement.ReviewGateConfigurations");

        group.MapPut("/{batchId:guid}/{gateType}", UpsertAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Tạo hoặc cập nhật deadline của một cổng thẩm định")
            .WithDescription("""
                Tạo mới hoặc thay thế cấu hình deadline của một cổng trong đợt khóa luận.
                `gateType` chỉ nhận `Syllabus` (Cổng 1 — đề cương chi tiết) hoặc `Midterm`
                (Cổng 2 — báo cáo giữa kỳ). Body phải dùng các thời điểm UTC theo ISO 8601
                và thỏa thứ tự nghiêm ngặt:
                `initialSubmissionDeadlineUtc < facultyResponseDeadlineUtc < finalClosureDeadlineUtc`.

                Đây là thao tác idempotent: cùng `batchId` và `gateType` sẽ cập nhật bản ghi
                hiện có, thay vì tạo thêm bản sao. Mọi lần tạo/cập nhật thành công đều được
                ghi vào audit outbox trong cùng transaction với dữ liệu nghiệp vụ.

                Yêu cầu Bearer token và permission `thesis.review-gate-config.manage`, được
                cấp mặc định cho `Admin` và `AcademicAffairs` (Phòng Đào tạo).
                - 200 OK: deadline đã được tạo hoặc cập nhật.
                - 400 Bad Request: cổng, định dạng UTC hoặc thứ tự ba deadline không hợp lệ.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không có quyền cấu hình deadline.
                - 404 Not Found: không tìm thấy đợt khóa luận.
                - 409 Conflict: cấu hình vừa bị một request khác thay đổi.
                """)
            .Produces<ApiResponse<ReviewGateConfigurationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapGet("/{batchId:guid}", GetByBatchAsync)
            .RequireAuthorization(ManagePolicy)
            .WithSummary("Tra cứu deadline của hai cổng thẩm định trong một đợt")
            .WithDescription("""
                Trả về các cấu hình deadline đã có của Cổng 1 (`Syllabus`) và Cổng 2 (`Midterm`)
                theo đợt khóa luận. Danh sách có thể rỗng hoặc mới chỉ có một cổng nếu PĐT chưa
                cấu hình đủ hai cổng; đây không phải lỗi. Kết quả được sắp xếp theo loại cổng.

                Yêu cầu Bearer token và permission `thesis.review-gate-config.manage`, được
                cấp mặc định cho `Admin` và `AcademicAffairs` (Phòng Đào tạo).
                - 200 OK: danh sách cấu hình, có thể rỗng.
                - 401 Unauthorized: chưa xác thực.
                - 403 Forbidden: không có quyền xem deadline cấu hình.
                - 404 Not Found: không tìm thấy đợt khóa luận.
                """)
            .Produces<ApiResponse<IReadOnlyList<ReviewGateConfigurationDto>>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> UpsertAsync(
        Guid batchId,
        ReviewGateType gateType,
        [FromBody] UpsertReviewGateConfigurationRequest request,
        UpsertReviewGateConfigurationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new UpsertReviewGateConfigurationCommand(
                batchId,
                gateType,
                request.InitialSubmissionDeadlineUtc,
                request.FacultyResponseDeadlineUtc,
                request.FinalClosureDeadlineUtc),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetByBatchAsync(
        Guid batchId,
        GetReviewGateConfigurationsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new GetReviewGateConfigurationsQuery(batchId),
            cancellationToken)).ToHttpResult();

    public sealed record UpsertReviewGateConfigurationRequest(
        DateTime InitialSubmissionDeadlineUtc,
        DateTime FacultyResponseDeadlineUtc,
        DateTime FinalClosureDeadlineUtc);
}
