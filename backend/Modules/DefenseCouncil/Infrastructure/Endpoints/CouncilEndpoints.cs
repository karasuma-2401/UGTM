using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.ApproveCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;
using UGTM.Modules.DefenseCouncil.Application.Councils.CheckCouncilDiscrepancy;
using UGTM.Modules.DefenseCouncil.Application.Councils.CreateCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMinuteWorkspace;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilScore;

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
            .WithSummary("Khởi tạo dự thảo Hội đồng bảo vệ KLTN")
            .WithDescription("""
                Khởi tạo một hội đồng bảo vệ khóa luận tốt nghiệp mới ở trạng thái dự thảo (`Draft`), xác định thời gian tổ chức, ca bảo vệ và phòng thi.
                
                ### Tham số Request Body (`CreateCouncilInputDto`):
                - `batchId`: GUID định danh đợt KLTN (bắt buộc, đợt KLTN phải đang tồn tại trong hệ thống).
                - `councilCode`: Mã định danh hội đồng (chuỗi không rỗng, tối đa 50 ký tự, ví dụ: 'HD01', 'HD-KLTN-01'). Hệ thống tự động trim khoảng trắng và kiểm tra không trùng lặp trong cùng một đợt KLTN (so sánh không phân biệt hoa thường).
                - `defenseDate`: Ngày diễn ra buổi bảo vệ (định dạng `YYYY-MM-DD`, ví dụ: '2026-12-20').
                - `timeSlot`: Ca bảo vệ (chuỗi không rỗng, tối đa 50 ký tự, ví dụ: '08:00 - 11:30', '13:30 - 17:00').
                - `room`: Phòng tổ chức bảo vệ (chuỗi không rỗng, tối đa 50 ký tự, ví dụ: 'A101', 'B202', 'Hội trường E').
                
                ### Quy tắc nghiệp vụ & Ràng buộc:
                1. **Kiểm tra đợt KLTN:** Đợt `batchId` phải tồn tại trong cơ sở dữ liệu học vụ.
                2. **Tính duy nhất của mã hội đồng:** Trong cùng một đợt KLTN, không được tồn tại hai hội đồng có cùng `councilCode`.
                3. **Chống trùng phòng thi (Anti-Double Booking):** Hệ thống đối chiếu `defenseDate`, `timeSlot` và `room` trên toàn bộ các hội đồng hiện có (tự động chuẩn hóa khoảng trắng và không phân biệt hoa thường) để chống việc xếp trùng phòng trong cùng một ca thi.
                4. **Trạng thái ban đầu:** Hội đồng được tạo với trạng thái ban đầu là `Draft` (Dự thảo).
                
                ### Phân quyền truy cập (RBAC):
                Yêu cầu Bearer token của người dùng có một trong các vai trò: `Faculty` (Văn phòng Khoa), `AcademicAffairs` (Phòng Đào tạo), hoặc `Admin` (Quản trị viên).
                
                ### Mã lỗi và phản hồi:
                - **200 OK:** Khởi tạo thành công, trả về `ApiResponse<CouncilResponseDto>` chứa thông tin hội đồng dự thảo.
                - **400 Bad Request:**
                  - `Council.InvalidInput`: Mã hội đồng, ca thi hoặc phòng thi bị để trống/chỉ chứa khoảng trắng.
                - **401 Unauthorized:** Người dùng chưa đăng nhập hoặc thiếu Bearer token.
                - **403 Forbidden:**
                  - `Council.Forbidden`: Người dùng không có quyền quản lý hội đồng (ví dụ: vai trò `Student`).
                - **404 Not Found:**
                  - `Council.BatchNotFound`: Không tìm thấy đợt KLTN tương ứng với `batchId` cung cấp.
                - **409 Conflict:**
                  - `Council.DuplicateCode`: Mã hội đồng đã tồn tại trong đợt KLTN này.
                  - `Council.RoomScheduleConflict`: Phòng thi đã được xếp cho một hội đồng khác trong cùng ngày và ca bảo vệ.
                """)
            .Produces<ApiResponse<CouncilResponseDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.Councils.Create");

        // 2. POST /api/councils/{id}/members
        group.MapPost("/{id:guid}/members", AddCouncilMembersAsync)
            .RequireAuthorization()
            .WithSummary("Thêm thành viên vào Hội đồng bảo vệ (Chủ tịch, Thư ký, Ủy viên)")
            .WithDescription("""
                Thêm một hoặc nhiều thành viên vào hội đồng bảo vệ khóa luận tốt nghiệp.
                Hỗ trợ truyền danh sách nhiều thành viên qua mảng `members: [{ userId, role }]` hoặc truyền lẻ một thành viên qua `userId`, `role`.
                
                ### Tham số URL:
                - `id`: GUID định danh của hội đồng bảo vệ cần thêm thành viên.
                
                ### Tham số Request Body (`AddCouncilMembersInputDto`):
                - `members`: Danh sách thành viên (tùy chọn nếu truyền lẻ). Mỗi phần tử gồm:
                  - `userId`: GUID định danh của giảng viên trong hệ thống (tài khoản phải tồn tại và đang hoạt động `Active`).
                  - `role`: Vai trò trong hội đồng (`Chairman` / `0`: Chủ tịch, `Secretary` / `1`: Thư ký, `Member` / `2`: Ủy viên).
                - `userId`: GUID của giảng viên (nếu truyền lẻ một thành viên).
                - `role`: Vai trò của giảng viên (nếu truyền lẻ một thành viên).
                
                ### Quy tắc nghiệp vụ & Ràng buộc cứng:
                1. **Trạng thái hội đồng:** Chỉ được phép chỉnh sửa danh sách thành viên khi hội đồng đang ở trạng thái dự thảo (`Draft`). Nếu hội đồng đã được duyệt (`Active` hoặc `Completed`), thao tác sẽ bị khóa bảo vệ.
                2. **Cơ cấu thành viên chuẩn:**
                   - Tối đa **01 Chủ tịch (`Chairman`)** và tối đa **01 Thư ký (`Secretary`)** trong một hội đồng.
                   - Số lượng Ủy viên (`Member`): 0 hoặc nhiều (`>= 0`).
                   - Đáp ứng cả mô hình Hội đồng 3 người (Chủ tịch, Thư ký, Ủy viên độc lập) lẫn mô hình Hội đồng 2 người (Chủ tịch, Thư ký, không có Ủy viên độc lập).
                3. **Chống xung đột lợi ích (Conflict of Interest - Ràng buộc cứng):**
                   - Giảng viên được chọn tuyệt đối **KHÔNG ĐƯỢC là Giảng viên hướng dẫn (GVHD)** của bất kỳ nhóm đề tài nào đã được gán vào hội đồng này.
                   - Nếu vi phạm, thao tác lập tức bị chặn với mã 409 Conflict (`Council.MemberIsGroupAdvisor`).
                   - **Trường hợp GVPB kiêm Ủy viên:** Hệ thống hoàn toàn cho phép Giảng viên phản biện (GVPB) tham gia làm Ủy viên/Thư ký của hội đồng (sẵn sàng kết nối với công thức tính điểm hệ số chia 6).
                4. **Chống trùng lịch giảng viên (Anti-Double Booking):**
                   - Hệ thống kiểm tra lịch làm việc của tất cả giảng viên được chọn: một giảng viên không thể tham gia hai hội đồng khác nhau có cùng ngày (`defenseDate`) và cùng ca bảo vệ (`timeSlot`).
                5. **Tính nguyên tử (All-or-Nothing):** Nếu bất kỳ thành viên nào trong danh sách gửi lên vi phạm (trùng lịch, xung đột lợi ích, tài khoản không tồn tại, trùng vai trò), toàn bộ request sẽ bị từ chối, không lưu một phần dữ liệu.
                
                ### Phân quyền truy cập (RBAC):
                Yêu cầu Bearer token của người dùng có vai trò `Faculty`, `AcademicAffairs`, hoặc `Admin`.
                
                ### Mã lỗi và phản hồi:
                - **200 OK:** Thêm thành viên thành công, trả về `ApiResponse<IReadOnlyList<CouncilMemberResponseDto>>` kèm thông tin chi tiết (họ tên, email, mã cán bộ, vai trò).
                - **400 Bad Request:**
                  - `Council.EmptyMembers`: Danh sách thành viên gửi lên bị rỗng.
                - **401 Unauthorized:** Chưa đăng nhập hoặc token không hợp lệ.
                - **403 Forbidden:**
                  - `Council.Forbidden`: Người dùng không đủ quyền thực hiện thao tác.
                - **404 Not Found:**
                  - `Council.NotFound`: Không tìm thấy hội đồng bảo vệ tương ứng với `id`.
                  - `Council.MemberNotFound`: Không tìm thấy thông tin giảng viên hoặc tài khoản giảng viên đang bị vô hiệu hóa.
                - **409 Conflict:**
                  - `Council.NotInDraftStatus`: Hội đồng không ở trạng thái dự thảo (`Draft`), không thể thay đổi thành viên.
                  - `Council.DuplicateChairman`: Hội đồng chỉ được phép có tối đa 01 Chủ tịch.
                  - `Council.DuplicateSecretary`: Hội đồng chỉ được phép có tối đa 01 Thư ký.
                  - `Council.DuplicateMemberInRequest`: Có giảng viên bị lặp lại nhiều lần trong cùng một request gửi lên.
                  - `Council.DuplicateMember`: Giảng viên đã là thành viên trong hội đồng này trước đó.
                  - `Council.MemberIsGroupAdvisor`: Giảng viên là GVHD của nhóm đề tài trong hội đồng, vi phạm quy định chống xung đột lợi ích.
                  - `Council.MemberScheduleConflict`: Giảng viên đã có lịch tham gia hội đồng khác trong cùng ca và ngày bảo vệ.
                """)
            .Produces<ApiResponse<IReadOnlyList<CouncilMemberResponseDto>>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.Councils.AddMembers");

        // 3. POST /api/councils/{id}/assignments
        group.MapPost("/{id:guid}/assignments", AssignCouncilGroupsAsync)
            .RequireAuthorization()
            .WithSummary("Gán nhóm đề tài sinh viên vào Hội đồng bảo vệ")
            .WithDescription("""
                Gán một hoặc nhiều nhóm đề tài sinh viên vào hội đồng bảo vệ khóa luận tốt nghiệp kèm theo thứ tự bảo vệ (`orderNumber`) trong ca thi.
                Hỗ trợ truyền danh sách qua `assignments: [{ groupId, orderNumber }]` hoặc truyền lẻ qua `groupId`, `orderNumber`.
                
                ### Tham số URL:
                - `id`: GUID định danh của hội đồng bảo vệ tiếp nhận nhóm đề tài.
                
                ### Tham số Request Body (`AssignCouncilGroupsInputDto`):
                - `assignments`: Danh sách nhóm cần gán (tùy chọn nếu truyền lẻ). Mỗi phần tử gồm:
                  - `groupId`: GUID định danh của nhóm sinh viên (`StudentGroup`).
                  - `orderNumber`: Thứ tự bảo vệ của nhóm trong ca thi (số nguyên dương `>= 1`).
                - `groupId`: GUID của nhóm sinh viên (nếu truyền lẻ một nhóm).
                - `orderNumber`: Thứ tự bảo vệ (nếu truyền lẻ một nhóm, mặc định là 1 nếu bỏ trống).
                
                ### Quy tắc nghiệp vụ & Ràng buộc cứng:
                1. **Trạng thái hội đồng:** Chỉ được gán nhóm khi hội đồng đang ở trạng thái dự thảo (`Draft`).
                2. **Điều kiện nhóm sinh viên:**
                   - Nhóm phải đang tồn tại và thuộc cùng đợt KLTN (`batchId`) với hội đồng.
                   - Trạng thái của nhóm không được là `Stopped` (đã dừng thực hiện đề tài).
                   - Nhóm không bị Giảng viên phản biện đình chỉ bảo vệ: nếu đã có kết quả đánh giá phản biện (`ReviewerEvaluation`) thì `DefenseApproval` không được là `Rejected`.
                3. **Tính duy nhất của nhóm đề tài:** Mỗi nhóm đề tài chỉ được gán vào tối đa 01 hội đồng bảo vệ trong toàn hệ thống.
                4. **Thứ tự bảo vệ (`orderNumber`):** Bắt buộc `>= 1` và không được trùng lặp giữa các nhóm trong cùng một hội đồng.
                5. **Chống xung đột lợi ích (Conflict of Interest - Ràng buộc cứng):**
                   - Giảng viên hướng dẫn (GVHD) của nhóm đề tài sắp gán tuyệt đối **KHÔNG ĐƯỢC có mặt trong danh sách thành viên hiện tại của hội đồng** (cả vị trí Chủ tịch, Thư ký, Ủy viên).
                   - Nếu phát hiện GVHD đang là thành viên hội đồng, hệ thống sẽ từ chối với mã 409 Conflict (`Council.AdvisorIsInCouncil`).
                6. **Tính nguyên tử (All-or-Nothing):** Nếu bất kỳ nhóm nào trong request bị trùng, sai đợt, bị đình chỉ bảo vệ hoặc vi phạm xung đột lợi ích, toàn bộ danh sách gán nhóm sẽ bị từ chối.
                
                ### Phân quyền truy cập (RBAC):
                Yêu cầu Bearer token của người dùng có vai trò `Faculty`, `AcademicAffairs`, hoặc `Admin`.
                
                ### Mã lỗi và phản hồi:
                - **200 OK:** Gán nhóm thành công, trả về `ApiResponse<IReadOnlyList<CouncilAssignmentResponseDto>>` kèm thứ tự bảo vệ và tên đề tài tiếng Việt/tiếng Anh.
                - **400 Bad Request:**
                  - `Council.EmptyAssignments`: Danh sách nhóm đề tài gửi lên bị rỗng.
                  - `Council.InvalidOrderNumber`: Thứ tự bảo vệ bé hơn 1.
                  - `Council.GroupNotInBatch`: Nhóm đề tài không thuộc cùng đợt KLTN với hội đồng.
                  - `Council.GroupStopped`: Nhóm đề tài đã dừng thực hiện hoặc không hợp lệ để bảo vệ.
                - **401 Unauthorized:** Chưa đăng nhập hoặc token không hợp lệ.
                - **403 Forbidden:**
                  - `Council.Forbidden`: Người dùng không có quyền gán nhóm đề tài.
                - **404 Not Found:**
                  - `Council.NotFound`: Không tìm thấy hội đồng bảo vệ tương ứng với `id`.
                  - `Council.GroupNotFound`: Không tìm thấy thông tin nhóm đề tài sinh viên.
                - **409 Conflict:**
                  - `Council.NotInDraftStatus`: Hội đồng không ở trạng thái dự thảo (`Draft`).
                  - `Council.DuplicateGroupInRequest`: Có nhóm đề tài bị lặp lại trong cùng một request.
                  - `Council.DuplicateOrderNumber`: Thứ tự bảo vệ bị trùng lặp giữa các nhóm trong hội đồng.
                  - `Council.GroupAlreadyAssigned`: Nhóm đề tài đã được phân công vào một hội đồng bảo vệ khác.
                  - `Council.GroupDefenseRejectedByReviewer`: Nhóm đề tài đã bị giảng viên phản biện đánh giá không đủ điều kiện bảo vệ.
                  - `Council.AdvisorIsInCouncil`: Nhóm đề tài có GVHD đang là thành viên của hội đồng này, vi phạm quy định chống xung đột lợi ích.
                """)
            .Produces<ApiResponse<IReadOnlyList<CouncilAssignmentResponseDto>>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.Councils.AssignGroups");

        // 4. PUT /api/councils/{id}/approve
        group.MapPut("/{id:guid}/approve", ApproveCouncilAsync)
            .RequireAuthorization()
            .WithSummary("Phòng Đào tạo phê duyệt Quyết định thành lập Hội đồng")
            .WithDescription("""
                Chuyên viên Phòng Đào tạo thẩm định và ban hành Quyết định thành lập Hội đồng bảo vệ khóa luận tốt nghiệp, chính thức chuyển hội đồng từ trạng thái dự thảo (`Draft`) sang trạng thái hoạt động (`Active`).
                
                ### Tham số URL:
                - `id`: GUID định danh của hội đồng bảo vệ cần phê duyệt quyết định.
                
                ### Tham số Request Body (`ApproveCouncilInputDto`):
                - `decisionNumber`: Số quyết định thành lập hội đồng do Trường/Phòng Đào tạo ban hành (chuỗi bắt buộc không rỗng, tối đa 100 ký tự, ví dụ: '123/QĐ-ĐHCNTT').
                - `signedDate`: Ngày ký quyết định ban hành (tùy chọn, định dạng `YYYY-MM-DD`, ví dụ: '2026-12-01').
                - `decisionFileUrl`: Đường dẫn URL đến tệp số hóa của quyết định đính kèm (tùy chọn, tối đa 500 ký tự, ví dụ URL tệp PDF lưu trên S3/MinIO).
                
                ### Quy tắc nghiệp vụ & Điều kiện phê duyệt:
                1. **Tiền điều kiện trạng thái:** Hội đồng phải đang ở trạng thái dự thảo (`Draft`). Nếu đã được duyệt trước đó, hệ thống sẽ từ chối với lỗi `Council.AlreadyApproved`.
                2. **Cơ cấu thành viên tối thiểu:**
                   - Bắt buộc phải có **01 Chủ tịch (`Chairman`)**.
                   - Bắt buộc phải có **01 Thư ký (`Secretary`)**.
                   - Số lượng Ủy viên (`Member`): Tùy chọn (`>= 0`). Chấp nhận hội đồng 2 người nếu trường hợp phản biện kiêm nhiệm hoặc không có ủy viên độc lập.
                3. **Điều kiện đề tài:** Hội đồng bắt buộc phải có **ít nhất 01 nhóm đề tài** đã được gán vào (`Assignments.Count >= 1`).
                4. **Tái kiểm tra an toàn (Double-Check):**
                   - Tự động kiểm tra lại xung đột phòng thi (`RoomScheduleConflict`) tại thời điểm bấm duyệt.
                   - Tự động kiểm tra lại trùng lịch của tất cả thành viên hội đồng (`MemberScheduleConflict`).
                   - Tự động kiểm tra lại toàn bộ quy định chống xung đột lợi ích giữa GVHD của các nhóm và các thành viên hội đồng.
                5. **Ghi vết kiểm toán (Audit Outbox):**
                   - Sau khi phê duyệt thành công, hệ thống tự động sinh một sự kiện kiểm toán tích hợp `DefenseCouncil.Council.Approved` lưu vào Outbox table để đồng bộ dữ liệu và lưu vết người duyệt (`ApprovedBy`), thời gian duyệt (`ApprovedAt`), số quyết định, số lượng thành viên và số lượng đề tài.
                
                ### Phân quyền truy cập (RBAC):
                Chỉ dành riêng cho chuyên viên Phòng Đào tạo (`AcademicAffairs`) hoặc Quản trị viên hệ thống (`Admin`). Cán bộ Văn phòng Khoa (`Faculty`) hay Giảng viên (`Lecturer`) không có thẩm quyền duyệt quyết định này.
                
                ### Mã lỗi và phản hồi:
                - **200 OK:** Phê duyệt thành công, chuyển trạng thái hội đồng sang `Active`, trả về `ApiResponse<CouncilResponseDto>` chứa đầy đủ số quyết định và ngày ký.
                - **400 Bad Request:**
                  - `Council.DecisionNumberRequired`: Số quyết định thành lập hội đồng bị để trống hoặc chỉ chứa khoảng trắng.
                  - `Council.MissingChairman`: Hội đồng chưa có Chủ tịch.
                  - `Council.MissingSecretary`: Hội đồng chưa có Thư ký.
                  - `Council.NoAssignedGroups`: Hội đồng chưa có nhóm đề tài nào được phân công.
                - **401 Unauthorized:** Chưa đăng nhập hoặc token không hợp lệ.
                - **403 Forbidden:**
                  - `Council.Forbidden`: Người dùng không có vai trò Phòng Đào tạo (`AcademicAffairs`) hoặc Quản trị viên (`Admin`).
                - **404 Not Found:**
                  - `Council.NotFound`: Không tìm thấy hội đồng bảo vệ tương ứng với `id`.
                - **409 Conflict:**
                  - `Council.AlreadyApproved`: Hội đồng đã được phê duyệt quyết định thành lập trước đó.
                  - `Council.RoomScheduleConflict`: Phát hiện phòng thi bị trùng lịch với hội đồng khác tại thời điểm duyệt.
                  - `Council.MemberScheduleConflict`: Phát hiện giảng viên bị trùng lịch tham gia hội đồng khác tại thời điểm duyệt.
                  - `Council.AdvisorIsInCouncil`: Phát hiện có nhóm đề tài có GVHD ngồi trong hội đồng, vi phạm quy định chống xung đột lợi ích.
                """)
            .Produces<ApiResponse<CouncilResponseDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.Councils.Approve");

        // 5. GET /api/councils/schedule/{id}
        group.MapGet("/schedule/{id:guid}", GetCouncilScheduleAsync)
            .RequireAuthorization()
            .WithSummary("Tra cứu thông tin Hội đồng & Lịch bảo vệ (Sinh viên & Giảng viên)")
            .WithDescription("""
                Tra cứu chi tiết thông tin hội đồng bảo vệ, thời gian, phòng thi, danh sách các thành viên hội đồng và lịch bảo vệ của các nhóm đề tài sinh viên.
                Hỗ trợ tra cứu linh hoạt thông qua một tham số `{id}` duy nhất.
                
                ### Tham số URL:
                - `id`: GUID linh hoạt, hệ thống tự động phân giải theo thứ tự ưu tiên:
                  1. **`groupId`**: Tìm kiếm hội đồng mà nhóm sinh viên này được phân công bảo vệ.
                  2. **`studentId`**: Tìm kiếm nhóm sinh viên đang hoạt động mà sinh viên này tham gia, sau đó định vị hội đồng bảo vệ tương ứng.
                  3. **`councilId`**: Tra cứu trực tiếp theo mã định danh của hội đồng bảo vệ.
                
                ### Phân cấp quyền hiển thị dữ liệu (Visibility Rules):
                1. **Đối với Sinh viên (`Student`):**
                   - Nếu người dùng chỉ có vai trò `Student`, lịch bảo vệ **chỉ được phép xem khi hội đồng đã được Phòng Đào tạo phê duyệt chính thức** (trạng thái `Active` hoặc `Completed`).
                   - Nếu hội đồng đang trong giai đoạn dự thảo xếp lịch (`Draft`), API sẽ trả về lỗi **404 Not Found** (`Council.ScheduleNotPublished`) nhằm bảo mật và tránh gây hoang mang khi lịch chưa chốt chính thức.
                2. **Đối với Cán bộ / Giảng viên (`Faculty`, `AcademicAffairs`, `Lecturer`, `Admin`):**
                   - Được phép xem trước toàn bộ lịch bảo vệ ngay khi hội đồng còn ở trạng thái dự thảo (`Draft`) phục vụ công tác rà soát và chuẩn bị.
                
                ### Cấu trúc dữ liệu phản hồi (`CouncilScheduleResponseDto`):
                - **Thông tin hội đồng:** `councilId`, `batchId`, `councilCode`, `defenseDate`, `timeSlot`, `room`, `status`, `decisionNumber`, `signedDate`.
                - **Danh sách thành viên hội đồng (`members`):** `userId`, `fullName`, `email`, `userCode`, `role` (`Chairman`, `Secretary`, `Member`).
                - **Danh sách nhóm đề tài bảo vệ (`groups`):** Sắp xếp theo thứ tự bảo vệ `orderNumber`:
                  - `orderNumber`: Thứ tự ca bảo vệ của nhóm (1, 2, 3...).
                  - `topicId`, `topicTitleVi`, `topicTitleEn`: Thông tin đề tài khóa luận.
                  - `advisorId`, `advisorName`: Họ tên và thông tin Giảng viên hướng dẫn.
                  - `reviewerId`, `reviewerName`: Họ tên và thông tin Giảng viên phản biện.
                  - `isReviewerInCouncil`: Cờ boolean xác định Giảng viên phản biện có đồng thời là thành viên (Thư ký/Ủy viên) trong hội đồng này hay không (đặc thù phục vụ công thức tính điểm).
                  - `students`: Danh sách các sinh viên trong nhóm (`studentId`, `fullName`, `email`, `userCode`).
                
                ### Phân quyền truy cập (RBAC):
                Yêu cầu người dùng đã xác thực (Bearer token hợp lệ).
                
                ### Mã lỗi và phản hồi:
                - **200 OK:** Tra cứu thành công, trả về `ApiResponse<CouncilScheduleResponseDto>` đầy đủ thông tin hội đồng và lịch bảo vệ.
                - **401 Unauthorized:** Người dùng chưa đăng nhập hoặc token không hợp lệ.
                - **403 Forbidden:**
                  - `Council.Forbidden`: Không có quyền truy cập.
                - **404 Not Found:**
                  - `Council.NotFound`: Không tìm thấy hội đồng bảo vệ tương ứng với định danh tra cứu.
                  - `Council.ScheduleNotPublished`: Lịch bảo vệ của hội đồng chưa được Phòng Đào tạo phê duyệt công bố (áp dụng đối với người dùng là Sinh viên khi hội đồng còn ở trạng thái `Draft`).
                """)
            .Produces<ApiResponse<CouncilScheduleResponseDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .WithName("DefenseCouncil.Councils.GetSchedule");

        // 6. POST /api/councils/assignments/{assignmentId}/scores
        group.MapPost("/assignments/{assignmentId:guid}/scores", SubmitCouncilScoreAsync)
            .RequireAuthorization()
            .WithSummary("Chấm điểm chi tiết theo tiêu chí barem cho nhóm đề tài trong hội đồng")
            .WithDescription("""
                Cho phép thành viên hội đồng nhập điểm đánh giá chi tiết theo từng tiêu chí barem (`EvaluationDetail`) và nhận xét đánh giá (`feedback`) cho nhóm sinh viên bảo vệ khóa luận.
                
                ### Tham số URL:
                - `assignmentId`: GUID định danh phân công nhóm đề tài trong hội đồng bảo vệ (`CouncilAssignment`).
                
                ### Tham số Request Body (`SubmitCouncilScoreInputDto`):
                - `details`: Danh sách điểm chi tiết theo tiêu chí barem (bắt buộc, không được để trống). Mỗi phần tử gồm:
                  - `criteriaId`: GUID định danh tiêu chí đánh giá (`GradingCriteria`). Tiêu chí phải thuộc đợt KLTN của hội đồng.
                  - `score`: Điểm số đạt được của tiêu chí (số thực, `0 <= score <= criteria.MaxScore`).
                - `feedback`: Nhận xét, đánh giá chuyên môn của thành viên hội đồng (tùy chọn, chuỗi văn bản tự do).
                
                ### Quy tắc nghiệp vụ & Ràng buộc cứng:
                1. **Nhận diện thành viên & Phân quyền:**
                   - Hệ thống tự động nhận diện thành viên hội đồng từ tài khoản đang đăng nhập (`CurrentUser.UserId`).
                   - Người dùng bắt buộc phải là một thành viên có tên trong Hội đồng bảo vệ (`CouncilMember`). Nếu không thuộc hội đồng, bị từ chối với lỗi 403 Forbidden (`Council.UserNotCouncilMember`).
                2. **Trạng thái hội đồng:**
                   - Hội đồng bắt buộc phải đang ở trạng thái đã được phê duyệt hoạt động (`Active`). Nếu hội đồng đang ở trạng thái dự thảo (`Draft`) hoặc đã hủy, bị từ chối với lỗi 409 Conflict (`Council.NotActiveForScoring`).
                3. **Xử lý đặc thù GVPB kiêm Ủy viên Hội đồng (TH1 - BR-07):**
                   - Nếu thành viên hội đồng đồng thời là **Giảng viên phản biện (GVPB)** của nhóm đề tài này, hệ thống tự động nhận diện và **chặn không cho phép nhập điểm hội đồng lần thứ hai**, tránh việc chấm trùng lặp gây sai lệch kết quả.
                   - Vi phạm quy tắc này sẽ lập tức trả về lỗi **409 Conflict** (`Council.ReviewerCouncilMemberCannotScoreTwice`).
                   - Điểm đánh giá tại buổi bảo vệ của giảng viên này sẽ được tự động sử dụng từ phiếu đánh giá phản biện (`ReviewerEvaluation`) để đưa vào công thức tính điểm tổng kết.
                4. **Kiểm tra tiêu chí barem:**
                   - Điểm của từng tiêu chí không được nhỏ hơn 0 và không được vượt quá `MaxScore` tương ứng.
                   - Bắt buộc phải nhập đầy đủ tất cả các tiêu chí trong barem đánh giá của đợt KLTN.
                   - Tổng điểm của thành viên (`CouncilScore.Score`) bằng tổng điểm các tiêu chí thành phần: `totalScore = Sum(details.Score)` (làm tròn 2 chữ số thập phân, `0 <= totalScore <= 10`).
                5. **Cập nhật điểm & Khóa biên bản:**
                   - Thành viên được phép gọi lại API để cập nhật lại điểm (Rescore/Idempotent update) khi biên bản hội đồng chưa bị khóa.
                   - Nếu biên bản đã bị khóa (`CouncilMinute.LockStatus == Locked`), mọi hành động chấm hoặc sửa điểm đều bị chặn với mã 409 Conflict (`Council.MinuteLocked`).
                6. **Tự động tổng kết & Đồng bộ ScoreSummary:**
                   - Khi tất cả thành viên bắt buộc của hội đồng đã chấm xong, hệ thống tự động cập nhật trạng thái biên bản sang `AllScoresEntered`, tính điểm trung bình của Hội đồng (`AverageCouncilScore`), và tính điểm tổng kết cuối cùng (`FinalScore`) theo công thức chuẩn (`ThesisGradeCalculator`), đồng bộ vào bảng tổng kết điểm `ScoreSummary`.
                
                ### Mã lỗi và phản hồi:
                - **200 OK:** Chấm điểm thành công, trả về `ApiResponse<CouncilScoreResponseDto>` chứa tổng điểm và chi tiết từng tiêu chí.
                - **400 Bad Request:**
                  - `Council.CriteriaRequired`: Danh sách tiêu chí bị để trống.
                  - `Council.CriteriaNotFound`: Tiêu chí không tồn tại hoặc không thuộc đợt KLTN của hội đồng.
                  - `Council.CriteriaScoreNegative`: Điểm tiêu chí nhỏ hơn 0.
                  - `Council.ScoreExceedsMaxScore`: Điểm tiêu chí vượt quá điểm tối đa cho phép.
                  - `Council.MissingRequiredCriteria`: Thiếu điểm của tiêu chí bắt buộc trong barem.
                - **401 Unauthorized:** Người dùng chưa đăng nhập hoặc thiếu Bearer token.
                - **403 Forbidden:**
                  - `Council.Forbidden`: Người dùng không đủ quyền hạn.
                  - `Council.UserNotCouncilMember`: Người dùng không phải thành viên của hội đồng này.
                - **404 Not Found:**
                  - `Council.AssignmentNotFound`: Không tìm thấy phân công nhóm đề tài trong hội đồng.
                - **409 Conflict:**
                  - `Council.NotActiveForScoring`: Hội đồng chưa được phê duyệt Active để chấm điểm.
                  - `Council.ReviewerCouncilMemberCannotScoreTwice`: GVPB kiêm thành viên hội đồng bị chặn chấm điểm lần thứ hai.
                  - `Council.MinuteLocked`: Biên bản hội đồng đã bị khóa, không thể chấm hoặc sửa điểm.
                """)
            .Produces<ApiResponse<CouncilScoreResponseDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.Councils.SubmitScore");

        // 7. GET /api/councils/assignments/{assignmentId}/discrepancy-check
        group.MapGet("/assignments/{assignmentId:guid}/discrepancy-check", CheckCouncilDiscrepancyAsync)
            .RequireAuthorization()
            .WithSummary("Kiểm tra sai lệch điểm giữa các thành viên hội đồng tại buổi bảo vệ")
            .WithDescription("""
                Tính toán và kiểm tra độ lệch điểm số giữa các thành viên tham gia đánh giá tại buổi bảo vệ của nhóm đề tài.
                Nếu khoảng cách giữa điểm cao nhất và điểm thấp nhất từ 2.0 điểm trở lên (>= 2.0), hệ thống sẽ bật cờ cảnh báo (Warning flag) để Thư ký và Hội đồng tiến hành thảo luận lại trước khi chốt biên bản.
                
                ### Tham số URL:
                - `assignmentId`: GUID định danh phân công nhóm đề tài trong hội đồng bảo vệ (`CouncilAssignment`).
                
                ### Quy tắc tính toán & Cảnh báo sai lệch điểm (Discrepancy Warning):
                1. **Tập điểm đánh giá buổi bảo vệ:**
                   - Gồm điểm số của tất cả các thành viên hội đồng đã chấm (`CouncilScore`).
                   - Trong trường hợp Giảng viên phản biện kiêm nhiệm thành viên hội đồng: Điểm phản biện (`ReviewerEvaluation.Score`) của giảng viên này sẽ được tự động đưa vào tập điểm so sánh tại buổi bảo vệ.
                2. **Điều kiện kích hoạt cảnh báo:**
                   - Độ lệch điểm: `scoreDifference = Math.Round(maxScore - minScore, 2)`.
                   - Ngưỡng cảnh báo: `warningThreshold = 2.0` điểm.
                   - Khi có từ 2 điểm số trở lên và `scoreDifference >= 2.0`:
                     - Cờ `isDiscrepancyDetected = true`.
                     - Kèm theo thông báo cảnh báo rõ ràng `warningMessage` để Thư ký và Hội đồng thảo luận lại.
                   - Nếu `scoreDifference < 2.0`: Cờ `isDiscrepancyDetected = false` và `warningMessage = null`.
                3. **Thông tin tiến độ chấm điểm:**
                   - Trả về cờ `allScoresSubmitted` xác định tất cả thành viên bắt buộc đã chấm xong hay chưa.
                   - Trả về điểm trung bình Hội đồng `averageCouncilScore` và điểm tổng kết cuối cùng `finalScore` (nếu đã đủ điều kiện tính toán).
                   - Trả về danh sách chi tiết điểm của từng thành viên (`memberScores`), gồm họ tên, vai trò trong hội đồng, điểm đã chấm, nhận xét, và cờ đánh dấu GVPB kiêm nhiệm (`isBlockedFromScoring = true`).
                
                ### Phân quyền truy cập (RBAC):
                Yêu cầu Bearer token của người dùng đã xác thực (Thành viên Hội đồng, Thư ký, Khoa, Phòng Đào tạo hoặc Quản trị viên).
                
                ### Mã lỗi và phản hồi:
                - **200 OK:** Tra cứu thành công, trả về `ApiResponse<CouncilDiscrepancyResponseDto>` chứa trạng thái cảnh báo và bảng điểm chi tiết.
                - **401 Unauthorized:** Người dùng chưa đăng nhập hoặc token không hợp lệ.
                - **403 Forbidden:** Không có quyền truy cập.
                - **404 Not Found:**
                  - `Council.AssignmentNotFound`: Không tìm thấy phân công nhóm đề tài trong hội đồng.
                """)
            .Produces<ApiResponse<CouncilDiscrepancyResponseDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .WithName("DefenseCouncil.Councils.CheckDiscrepancy");

        // 8. GET /api/councils/assignments/{assignmentId}/minute-workspace
        group.MapGet("/assignments/{assignmentId:guid}/minute-workspace", GetCouncilMinuteWorkspaceAsync)
            .RequireAuthorization()
            .WithSummary("Lấy toàn bộ không gian làm việc Biên bản Hội đồng")
            .WithDescription("""
                Truy xuất toàn bộ dữ liệu cần thiết cho màn hình Biên bản Hội đồng của một nhóm đề tài trong buổi bảo vệ, bao gồm 4 khối dữ liệu:
                
                ### 1. Khối thông tin phân công (`assignment_info`):
                - `assignment_id`: Mã định danh phân công nhóm vào hội đồng.
                - `council_id`: Mã định danh hội đồng.
                - `council_code`: Mã hội đồng (ví dụ: 'HĐ01').
                - `group_id`: Mã định danh nhóm đề tài.
                - `group_code`: Mã nhóm đề tài hiển thị (ví dụ: 'N01').
                - `chairman_name`: Họ tên Chủ tịch hội đồng.
                - `secretary_name`: Họ tên Thư ký hội đồng.
                - `lock_status`: Trạng thái khóa biên bản hiện tại (`Open`, `AllScoresEntered`, `Locked`, `UnlockedByOverride`).
                - `student_feedback`: Nội dung nhận xét/góp ý dành cho sinh viên đã lưu trước đó.

                ### 2. Khối danh sách tất cả đầu điểm (`score_items`):
                - Danh sách các đầu điểm gồm GVHD (hệ số 2), GVPB (hệ số 2), Chủ tịch (hệ số 1), Thư ký (hệ số 1), và Ủy viên (hệ số 1).
                - Xử lý đặc thù GVPB kiêm Ủy viên: GVPB hệ số 2; Ủy viên hiển thị `status_text = 'Chỉ tham dự'`, `score = null`, `weight = 0`, `category = 'Không thiếu điểm'`, `is_dual_role_reviewer = true`.
                - Mỗi đầu điểm bao gồm cờ `is_ignored_in_calculation` cho biết đầu điểm có đang bị loại khỏi công thức tính hay không.

                ### 3. Khối thông tin sai biệt (`discrepancy_summary`):
                - `has_discrepancy`: Phát hiện độ lệch giữa điểm cao nhất và thấp nhất >= 2.0.
                - `score_difference`: Giá trị chênh lệch điểm.
                - `chairman_conclusion`: Kết luận của Chủ tịch về việc công nhận hoặc loại điểm.
                - `secretary_marking`: Lựa chọn đánh dấu của Thư ký (`RejectedAsPerMinute`, `AcceptedAsPerMinute`, hoặc `None`).
                - `ignored_component_code`: Mã đầu điểm bị loại (nếu có).
                - `is_ignored_in_calculation`: Cờ boolean tổng thể có loại điểm hay không.

                ### 4. Khối diễn giải tính điểm trước làm tròn (`calculation_preview`):
                - Điểm trước điều chỉnh (`raw_total`, `raw_divisor`, `raw_average` giữ 6 chữ số thập phân).
                - Điểm sau điều chỉnh (`adjusted_total`, `adjusted_divisor`, `adjusted_average`).
                - `formula_explanation`: Chuỗi giải trình chi tiết từng thành phần để hiển thị UI và hỗ trợ nút 'Xem công thức'.
                - `is_ready_to_lock`: Cờ xác nhận biên bản đã đủ điều kiện để khóa (đủ tất cả điểm bắt buộc và đã xử lý xong sai biệt nếu có).

                ### Phân quyền truy cập (RBAC):
                Yêu cầu Bearer token của người dùng đã xác thực (Thành viên Hội đồng, Thư ký, Giảng viên, Khoa hoặc Quản trị viên).

                ### Mã lỗi và phản hồi:
                - **200 OK:** Truy xuất thành công `ApiResponse<CouncilMinuteWorkspaceResponseDto>`.
                - **401 Unauthorized:** Chưa xác thực.
                - **403 Forbidden:** Không có quyền truy cập.
                - **404 Not Found:** `Council.AssignmentNotFound`: Phân công hội đồng không tồn tại.
                """)
            .Produces<ApiResponse<CouncilMinuteWorkspaceResponseDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .WithName("DefenseCouncil.Councils.GetMinuteWorkspace");

        // 9. PUT /api/councils/assignments/{assignmentId}/minute
        group.MapPut("/assignments/{assignmentId:guid}/minute", SaveCouncilMinuteAsync)
            .RequireAuthorization()
            .WithSummary("Lưu nháp Biên bản Hội đồng & Xem trước công thức tính điểm")
            .WithDescription("""
                Hỗ trợ Thư ký lưu nháp biên bản hội đồng hoặc xem trước kết quả tính điểm khi thay đổi đánh dấu xử lý sai biệt:

                ### Tham số Query:
                - `preview`: Boolean (`true`/`false`, mặc định `false`).
                  - Khi `preview=true`: Chỉ thực hiện tính toán trên bộ nhớ và trả về `calculation_preview` cùng `discrepancy_summary` mới nhất, **không ghi xuống DB**.
                  - Khi `preview=false` (lưu thật): Chỉ cho phép **Thư ký (`CouncilMemberRole.Secretary`)** của hội đồng đó (hoặc Admin) thực hiện khi biên bản chưa bị `Locked`.

                ### Tham số Request Body (`SaveCouncilMinuteInputDto`):
                - `secretary_marking`: Đánh dấu của Thư ký (`RejectedAsPerMinute`, `AcceptedAsPerMinute`, hoặc `None`).
                - `target_component_code`: Mã đầu điểm cần loại (bắt buộc khi chọn `RejectedAsPerMinute`, ví dụ: 'REVIEWER', 'ADVISOR', 'COUNCIL_CHAIRMAN', 'COUNCIL_SECRETARY', 'COUNCIL_MEMBER').
                - `student_feedback`: Nội dung nhận xét/góp ý dành cho sinh viên (lưu vào `RequiredRevisions` và `GeneralFeedback` của bảng `CouncilMinute`).
                - `chairman_conclusion`: Kết luận bổ sung của Chủ tịch (tuỳ chọn).

                ### Quy tắc nghiệp vụ & Ràng buộc:
                1. **Bắt buộc chỉ định đầu điểm khi từ chối:** Nếu `secretary_marking == 'RejectedAsPerMinute'`, client bắt buộc phải truyền `target_component_code` hợp lệ.
                2. **Quyền hạn lưu thật:** Chỉ Thư ký của hội đồng (hoặc Admin) mới có quyền lưu thật. Chủ tịch hoặc Ủy viên chỉ có thể gọi ở chế độ preview.
                3. **Kiểm tra trạng thái khóa:** Không thể lưu khi biên bản đã ở trạng thái `Locked`.
                4. **Cập nhật và đồng bộ:** Hệ thống cập nhật cờ `IsIgnoredInCalculation` trên đầu điểm được chỉ định, lưu nhận xét sinh viên và tự động đồng bộ lại `ScoreSummary` cùng `ScoreSummaryComponent`.

                ### Mã lỗi và phản hồi:
                - **200 OK:** Lưu nháp hoặc xem trước thành công `ApiResponse<SaveCouncilMinuteResponseDto>`.
                - **400 Bad Request:**
                  - `Council.TargetComponentRequiredForRejection`: Thiếu `target_component_code` khi chọn loại điểm.
                  - `Council.InvalidTargetComponent`: Mã đầu điểm cần loại không hợp lệ.
                - **401 Unauthorized:** Chưa xác thực.
                - **403 Forbidden:**
                  - `Council.OnlySecretaryCanSaveMinute`: Người dùng không phải Thư ký hội đồng khi gọi lưu thật.
                - **404 Not Found:** `Council.AssignmentNotFound`.
                - **409 Conflict:**
                  - `Council.MinuteLocked`: Biên bản đã bị khóa.
                """)
            .Produces<ApiResponse<SaveCouncilMinuteResponseDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
            .WithName("DefenseCouncil.Councils.SaveMinute");

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

    private static async Task<IResult> SubmitCouncilScoreAsync(
        [FromRoute] Guid assignmentId,
        [FromBody] SubmitCouncilScoreInputDto body,
        [FromServices] SubmitCouncilScoreHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new SubmitCouncilScoreCommand(
            assignmentId,
            body.Details,
            body.Feedback);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> CheckCouncilDiscrepancyAsync(
        [FromRoute] Guid assignmentId,
        [FromServices] CheckCouncilDiscrepancyHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new CheckCouncilDiscrepancyQuery(assignmentId);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetCouncilMinuteWorkspaceAsync(
        [FromRoute] Guid assignmentId,
        [FromServices] GetCouncilMinuteWorkspaceHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCouncilMinuteWorkspaceQuery(assignmentId);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> SaveCouncilMinuteAsync(
        [FromRoute] Guid assignmentId,
        [FromQuery] bool? preview,
        [FromBody] SaveCouncilMinuteInputDto body,
        [FromServices] SaveCouncilMinuteHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new SaveCouncilMinuteCommand(
            assignmentId,
            body.SecretaryMarking,
            body.TargetComponentCode,
            body.StudentFeedback,
            body.ChairmanConclusion,
            preview ?? false);

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToHttpResult();
    }
}
