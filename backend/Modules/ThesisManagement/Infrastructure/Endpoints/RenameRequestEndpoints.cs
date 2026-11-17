using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.CreateRenameRequest;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.Dtos;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.GetGroupRenameRequests;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.GetRenameRequests;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAcademicAffairs;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAdvisor;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByFaculty;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class RenameRequestEndpoints
{
    public static IEndpointRouteBuilder MapRenameRequestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/thesis")
            .WithTags("ThesisManagement.RenameRequests");

        group.MapPost("/groups/{groupId:guid}/rename-requests", CreateRenameRequestAsync)
            .RequireAuthorization()
            .WithSummary("Tạo đơn xin đổi tên đề tài (Sinh viên)")
            .WithDescription("""
                Sinh viên thuộc nhóm tạo đơn xin đổi tên đề tài trực tuyến trong thời hạn cho phép.

                ### Quy tắc nghiệp vụ:
                1. **Thời hạn (Tuần 3 - Tuần 10):** Đợt KLTN phải đang mở (`Open`) và thời điểm tạo đơn phải nằm trong khoảng từ ngày bắt đầu của Giai đoạn thực hiện (Stage 3) đến hạn chót của Cổng giữa kỳ (Stage 4). Quá hạn hoặc ngoài giai đoạn này sẽ bị từ chối với mã lỗi 400 (`RenameRequest.OutsideAllowedWindow`).
                2. **Thông tin đơn:** Bắt buộc nhập tên tiếng Việt mới (`NewTitleVi`, tối đa 300 ký tự) và lý do điều chỉnh (`Reason`). Tùy chọn nhập tên tiếng Anh mới (`NewTitleEn`, tối đa 350 ký tự).
                3. **Ràng buộc tên:** Tên mới không được trùng hoàn toàn với tên đề tài hiện tại (`RenameRequest.TitleUnchanged`).
                4. **Ràng buộc đơn chờ:** Mỗi nhóm chỉ được phép có tối đa 1 đơn đang chờ thẩm định (`Created`, `AdvisorConfirmed`, `FacultyApproved`). Nếu đơn trước đã bị từ chối hoặc hoàn tất, nhóm có thể tạo đơn mới trong thời hạn.
                5. **Phân quyền:** Chỉ thành viên đã xác nhận (`Confirmed`) của nhóm mới có quyền tạo đơn.
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("ThesisManagement.RenameRequests.Create");

        group.MapGet("/groups/{groupId:guid}/rename-requests", GetGroupRenameRequestsAsync)
            .RequireAuthorization()
            .WithSummary("Lấy lịch sử đơn xin đổi tên đề tài của nhóm")
            .WithDescription("""
                Lấy toàn bộ lịch sử các đơn xin đổi tên đề tài của một nhóm sinh viên, sắp xếp theo thời gian tạo mới nhất.
                Truy cập bởi thành viên nhóm, GVHD hướng dẫn đề tài, Văn phòng Khoa, Phòng Đào tạo hoặc Quản trị viên.
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("ThesisManagement.RenameRequests.GetByGroup");

        group.MapGet("/rename-requests", GetRenameRequestsAsync)
            .RequireAuthorization()
            .WithSummary("Tra cứu danh sách đơn xin đổi tên đề tài")
            .WithDescription("""
                Tra cứu danh sách đơn xin đổi tên đề tài với các bộ lọc tùy chọn theo `groupId` hoặc `status`.
                Truy cập bởi Cán bộ Văn phòng Khoa, Phòng Đào tạo, Giảng viên hoặc Quản trị viên.
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .WithName("ThesisManagement.RenameRequests.List");

        group.MapPost("/rename-requests/{requestId:guid}/advisor-review", ReviewByAdvisorAsync)
            .RequireAuthorization()
            .WithSummary("Cấp 1: GVHD thẩm định đơn đổi tên đề tài")
            .WithDescription("""
                Giảng viên hướng dẫn thẩm định đơn xin đổi tên đề tài của nhóm do mình hướng dẫn.

                ### Quy tắc nghiệp vụ:
                1. **Quyết định thẩm định:** `Decision` là `Approve` hoặc `Reject`.
                2. **Đồng ý (`Approve`):** Đơn chuyển sang trạng thái `AdvisorConfirmed` (chờ Cấp 2: Văn phòng Khoa duyệt).
                3. **Từ chối (`Reject`):** Bắt buộc nhập `RejectionReason`. Đơn chuyển sang trạng thái `RejectedByAdvisor`, kết thúc luồng duyệt và giữ nguyên tên đề tài ban đầu.
                4. **Phân quyền:** Chỉ chính xác GVHD của đề tài (hoặc Admin) mới có quyền thẩm định.
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("ThesisManagement.RenameRequests.AdvisorReview");

        group.MapPost("/rename-requests/{requestId:guid}/faculty-review", ReviewByFacultyAsync)
            .RequireAuthorization()
            .WithSummary("Cấp 2: Văn phòng Khoa thẩm định đơn đổi tên đề tài")
            .WithDescription("""
                Cán bộ Văn phòng Khoa thẩm định đơn xin đổi tên đề tài đã được GVHD đồng ý.

                ### Quy tắc nghiệp vụ:
                1. **Tiền điều kiện:** Đơn phải ở trạng thái `AdvisorConfirmed`.
                2. **Đồng ý (`Approve`):** Đơn chuyển sang trạng thái `FacultyApproved` (chờ Cấp 3: Phòng Đào tạo duyệt).
                3. **Từ chối (`Reject`):** Bắt buộc nhập `RejectionReason`. Đơn chuyển sang trạng thái `RejectedByFaculty`, kết thúc luồng duyệt và giữ nguyên tên đề tài ban đầu.
                4. **Phân quyền:** Cán bộ có vai trò `Faculty` hoặc quyền `thesis.review-gates.review` (hoặc Admin).
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("ThesisManagement.RenameRequests.FacultyReview");

        group.MapPost("/rename-requests/{requestId:guid}/academic-affairs-review", ReviewByAcademicAffairsAsync)
            .RequireAuthorization()
            .WithSummary("Cấp 3: Phòng Đào tạo phê duyệt chính thức đơn đổi tên đề tài")
            .WithDescription("""
                Chuyên viên Phòng Đào tạo phê duyệt chính thức đơn xin đổi tên đề tài đã qua 2 cấp duyệt.

                ### Quy tắc nghiệp vụ:
                1. **Tiền điều kiện:** Đơn phải ở trạng thái `FacultyApproved`.
                2. **Từ chối (`Reject`):** Bắt buộc nhập `RejectionReason`. Đơn chuyển sang trạng thái `RejectedByAA`, kết thúc luồng duyệt và giữ nguyên tên đề tài ban đầu.
                3. **Phê duyệt (`Approve`):**
                   - Đơn chuyển sang trạng thái `Completed`.
                   - Tự động cập nhật `TitleVi` và `TitleEn` mới vào bảng `topics.topic`.
                   - Tự động kích hoạt cờ `AllowSyllabusResubmission = true` cho nhóm sinh viên, cho phép sinh viên nộp lại bản Đề cương cập nhật.
                   - Ghi vết Audit Log hệ thống qua `IAuditLogService` với action `ThesisManagement.RenameRequest.Approved`, lưu tên cũ và mới vào Metadata.
                4. **Phân quyền:** Chuyên viên có vai trò `AcademicAffairs` (hoặc Admin).
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("ThesisManagement.RenameRequests.AcademicAffairsReview");

        return endpoints;
    }

    private static async Task<IResult> CreateRenameRequestAsync(
        [FromRoute] Guid groupId,
        [FromBody] CreateRenameRequestInputDto body,
        [FromServices] CreateRenameRequestHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateRenameRequestCommand(
            groupId,
            body.NewTitleVi,
            body.NewTitleEn,
            body.Reason);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetGroupRenameRequestsAsync(
        [FromRoute] Guid groupId,
        [FromServices] GetGroupRenameRequestsHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetGroupRenameRequestsQuery(groupId);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetRenameRequestsAsync(
        [FromQuery] Guid? groupId,
        [FromQuery] string? status,
        [FromServices] GetRenameRequestsHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetRenameRequestsQuery(groupId, status);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ReviewByAdvisorAsync(
        [FromRoute] Guid requestId,
        [FromBody] ReviewRenameRequestInputDto body,
        [FromServices] ReviewRenameRequestByAdvisorHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new ReviewRenameRequestByAdvisorCommand(
            requestId,
            body.Decision,
            body.RejectionReason);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ReviewByFacultyAsync(
        [FromRoute] Guid requestId,
        [FromBody] ReviewRenameRequestInputDto body,
        [FromServices] ReviewRenameRequestByFacultyHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new ReviewRenameRequestByFacultyCommand(
            requestId,
            body.Decision,
            body.RejectionReason);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ReviewByAcademicAffairsAsync(
        [FromRoute] Guid requestId,
        [FromBody] ReviewRenameRequestInputDto body,
        [FromServices] ReviewRenameRequestByAcademicAffairsHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new ReviewRenameRequestByAcademicAffairsCommand(
            requestId,
            body.Decision,
            body.RejectionReason);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }
}
