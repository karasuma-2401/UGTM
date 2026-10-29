using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Application.StudentGroups.Dtos;
using UGTM.Modules.ThesisManagement.Application.StudentGroups.GetStudentGroupDetail;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class StudentGroupEndpoints
{
    public static IEndpointRouteBuilder MapStudentGroupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/thesis/groups/{groupId:guid}/detail", GetDetailAsync)
            .WithTags("ThesisManagement.StudentGroups")
            .RequireAuthorization()
            .WithSummary("Xem thông tin chi tiết nhóm khóa luận")
            .WithDescription("""
                Trả thông tin nhóm, đề tài Việt/Anh, đợt, GVHD hiện tại và danh sách thành viên.
                Chỉ Student đã Confirmed trong nhóm, GVHD hiện được phân công cho đề tài,
                Faculty có thesis.review-gates.review hoặc Admin được xem. Thành viên Pending
                không có quyền xem chỉ nhờ lời mời. Không giới hạn theo deadline/trạng thái nhóm hoặc đợt.

                Dữ liệu theo mô hình BE hiện tại: không có mã nhóm/đề tài, học vị GVHD hoặc isLate.
                Tên đợt/tên người dùng và email thành viên có thể null nếu dữ liệu liên quan bị thiếu.
                Không trả token xác nhận, thông tin bảo mật hoặc quyền tải hồ sơ.
                Sinh viên có thể lấy groupId từ /api/thesis/topic-registrations/my-status.

                400: groupId rỗng; 401: chưa đăng nhập; 403: không có quyền; 404: không có nhóm;
                500: thiếu liên kết đề tài hoặc dữ liệu đợt của nhóm không nhất quán.
                """)
            .Produces<ApiResponse<StudentGroupDetailDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status500InternalServerError);
        return endpoints;
    }

    private static async Task<IResult> GetDetailAsync(
        Guid groupId, GetStudentGroupDetailHandler handler, CancellationToken cancellationToken)
        => (await handler.HandleAsync(new GetStudentGroupDetailQuery(groupId), cancellationToken)).ToHttpResult();
}
