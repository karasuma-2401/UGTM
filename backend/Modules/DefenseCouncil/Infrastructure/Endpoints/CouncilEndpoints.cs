using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.ApproveCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;
using UGTM.Modules.DefenseCouncil.Application.Councils.CreateCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

public static class CouncilEndpoints
{
    public static IEndpointRouteBuilder MapCouncilEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/councils")
            .WithTags("DefenseCouncil.Councils");

        // 1. POST /api/councils
        group.MapPost("/", CreateCouncilAsync)
            .RequireAuthorization()
            .WithSummary("Tạo mới hội đồng bảo vệ KLTN (Dự thảo)")
            .WithDescription("""
                Tạo mới một hội đồng bảo vệ khóa luận tốt nghiệp ở trạng thái dự thảo (`Draft`).
                
                ### Quy tắc nghiệp vụ:
                1. **Đợt KLTN:** `BatchId` phải tồn tại trong hệ thống.
                2. **Mã hội đồng:** `CouncilCode` không được trùng lặp trong cùng đợt KLTN.
                3. **Trùng lặp phòng thi:** Hệ thống kiểm tra `DefenseDate`, `TimeSlot`, và `Room` để chống việc xếp trùng phòng thi giữa các hội đồng.
                4. **Phân quyền:** Cán bộ Khoa (`Faculty`), Chuyên viên PĐT (`AcademicAffairs`), hoặc Quản trị viên (`Admin`).
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.Councils.Create");

        // 2. POST /api/councils/{id}/members
        group.MapPost("/{id:guid}/members", AddCouncilMembersAsync)
            .RequireAuthorization()
            .WithSummary("Thêm thành viên vào hội đồng bảo vệ")
            .WithDescription("""
                Thêm một hoặc nhiều thành viên vào hội đồng bảo vệ (Chủ tịch, Thư ký, Ủy viên).
                Hỗ trợ truyền danh sách `members: [{ userId, role }]` hoặc một thành viên lẻ `userId`, `role`.
                
                ### Quy tắc nghiệp vụ & Ràng buộc cứng:
                1. **Trạng thái hội đồng:** Chỉ được thêm/chỉnh sửa thành viên khi hội đồng đang ở trạng thái dự thảo (`Draft`).
                2. **Cơ cấu thành viên:** Tối đa 01 Chủ tịch (`Chairman`), tối đa 01 Thư ký (`Secretary`), và 0 hoặc nhiều Ủy viên (`Member`).
                3. **Chống xung đột lợi ích (Hard Validation):** Tuyệt đối không cho phép giảng viên hướng dẫn (GVHD) của các nhóm đề tài trong hội đồng tham gia làm thành viên của hội đồng đó (`409 Conflict`). Cho phép giảng viên phản biện (GVPB) làm Ủy viên nếu kiêm nhiệm.
                4. **Trùng lặp lịch giảng viên:** Giảng viên không được trùng lịch tham gia hội đồng khác trong cùng ngày (`DefenseDate`) và ca bảo vệ (`TimeSlot`).
                5. **Phân quyền:** `Faculty`, `AcademicAffairs`, `Admin`.
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.Councils.AddMembers");

        // 3. POST /api/councils/{id}/assignments
        group.MapPost("/{id:guid}/assignments", AssignCouncilGroupsAsync)
            .RequireAuthorization()
            .WithSummary("Gán nhóm đề tài sinh viên vào hội đồng bảo vệ")
            .WithDescription("""
                Gán một hoặc nhiều nhóm đề tài sinh viên vào hội đồng bảo vệ kèm thứ tự bảo vệ (`OrderNumber`).
                Hỗ trợ truyền danh sách `assignments: [{ groupId, orderNumber }]` hoặc một nhóm lẻ `groupId`, `orderNumber`.
                
                ### Quy tắc nghiệp vụ & Ràng buộc cứng:
                1. **Trạng thái hội đồng:** Chỉ được gán nhóm khi hội đồng đang ở trạng thái dự thảo (`Draft`).
                2. **Điều kiện nhóm:** Nhóm phải thuộc cùng đợt KLTN với hội đồng, chưa bị dừng (`Stopped`), và không bị giảng viên phản biện đình chỉ bảo vệ (`Rejected`).
                3. **Duy nhất:** Mỗi nhóm đề tài chỉ được gán vào tối đa 01 hội đồng bảo vệ trong toàn hệ thống.
                4. **Thứ tự bảo vệ:** `OrderNumber >= 1` và không được trùng nhau giữa các nhóm trong cùng hội đồng.
                5. **Chống xung đột lợi ích (Hard Validation):** GVHD của nhóm đề tài sắp gán tuyệt đối không được có mặt trong danh sách thành viên của hội đồng này (`409 Conflict`).
                6. **Phân quyền:** `Faculty`, `AcademicAffairs`, `Admin`.
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.Councils.AssignGroups");

        // 4. PUT /api/councils/{id}/approve
        group.MapPut("/{id:guid}/approve", ApproveCouncilAsync)
            .RequireAuthorization()
            .WithSummary("Phòng Đào tạo phê duyệt Quyết định thành lập Hội đồng")
            .WithDescription("""
                Chuyên viên Phòng Đào tạo phê duyệt chính thức Quyết định thành lập Hội đồng bảo vệ, chuyển hội đồng sang trạng thái hoạt động (`Active`).
                
                ### Quy tắc nghiệp vụ:
                1. **Tiền điều kiện:** Hội đồng phải ở trạng thái `Draft`.
                2. **Điều kiện cơ cấu tối thiểu:** Bắt buộc có đủ 01 Chủ tịch (`Chairman`) và 01 Thư ký (`Secretary`) (Ủy viên là tùy chọn `>= 0`).
                3. **Điều kiện đề tài:** Bắt buộc có ít nhất 01 nhóm đề tài được gán vào hội đồng.
                4. **Số quyết định:** Bắt buộc nhập `DecisionNumber`.
                5. **Xác thực an toàn:** Tự động tái kiểm tra lịch phòng, lịch giảng viên và chống xung đột lợi ích trước khi chốt duyệt.
                6. **Ghi vết (Audit Log):** Hệ thống tự động ghi nhận sự kiện phê duyệt hội đồng vào Outbox Audit Log.
                7. **Phân quyền:** Chỉ chuyên viên Phòng Đào tạo (`AcademicAffairs`) hoặc Quản trị viên (`Admin`).
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.Councils.Approve");

        // 5. GET /api/councils/schedule/{id}
        group.MapGet("/schedule/{id:guid}", GetCouncilScheduleAsync)
            .RequireAuthorization()
            .WithSummary("Lấy thông tin Hội đồng & Lịch bảo vệ cho Sinh viên và Giảng viên")
            .WithDescription("""
                Tra cứu chi tiết thông tin hội đồng, thời gian, phòng bảo vệ, danh sách thành viên hội đồng và các nhóm đề tài.
                Tham số `{id}` hỗ trợ tra cứu linh hoạt theo `groupId`, `studentId`, hoặc `councilId`.
                
                ### Quy tắc hiển thị:
                1. **Sinh viên:** Nếu người gọi chỉ có vai trò `Student`, lịch bảo vệ chỉ được trả về khi hội đồng đã được Phòng Đào tạo phê duyệt chính thức (`Active` hoặc `Completed`). Nếu hội đồng còn là dự thảo (`Draft`), hệ thống sẽ trả về 404 (`Council.ScheduleNotPublished`).
                2. **Cán bộ / Giảng viên:** Có thể xem trước lịch hội đồng ở trạng thái dự thảo.
                3. **Chi tiết trả về:** Gồm mã HĐ, ngày bảo vệ, ca thi, phòng, số quyết định, danh sách thành viên (họ tên, email, vai trò) và danh sách nhóm bảo vệ (thứ tự bảo vệ, tên đề tài VI/EN, tên GVHD, tên GVPB, cờ GVPB kiêm thành viên HĐ, danh sách sinh viên).
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DefenseCouncil.Councils.GetSchedule");

        return endpoints;
    }

    private static async Task<IResult> CreateCouncilAsync(
        [FromBody] CreateCouncilInputDto body,
        [FromServices] CreateCouncilHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateCouncilCommand(
            body.BatchId,
            body.CouncilCode,
            body.DefenseDate,
            body.TimeSlot,
            body.Room);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> AddCouncilMembersAsync(
        [FromRoute] Guid id,
        [FromBody] AddCouncilMembersInputDto body,
        [FromServices] AddCouncilMembersHandler handler,
        CancellationToken cancellationToken)
    {
        var members = new List<CouncilMemberInputDto>();
        if (body.Members != null && body.Members.Count > 0)
        {
            members.AddRange(body.Members);
        }
        else if (body.UserId.HasValue && body.Role.HasValue)
        {
            members.Add(new CouncilMemberInputDto(body.UserId.Value, body.Role.Value));
        }

        var command = new AddCouncilMembersCommand(id, members);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> AssignCouncilGroupsAsync(
        [FromRoute] Guid id,
        [FromBody] AssignCouncilGroupsInputDto body,
        [FromServices] AssignCouncilGroupsHandler handler,
        CancellationToken cancellationToken)
    {
        var assignments = new List<CouncilAssignmentInputDto>();
        if (body.Assignments != null && body.Assignments.Count > 0)
        {
            assignments.AddRange(body.Assignments);
        }
        else if (body.GroupId.HasValue)
        {
            assignments.Add(new CouncilAssignmentInputDto(body.GroupId.Value, body.OrderNumber ?? 1));
        }

        var command = new AssignCouncilGroupsCommand(id, assignments);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ApproveCouncilAsync(
        [FromRoute] Guid id,
        [FromBody] ApproveCouncilInputDto body,
        [FromServices] ApproveCouncilHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new ApproveCouncilCommand(
            id,
            body.DecisionNumber,
            body.SignedDate,
            body.DecisionFileUrl);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetCouncilScheduleAsync(
        [FromRoute] Guid id,
        [FromServices] GetCouncilScheduleHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCouncilScheduleQuery(id);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }
}
