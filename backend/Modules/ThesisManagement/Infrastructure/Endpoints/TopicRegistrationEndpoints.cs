using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.AdvisorAssignTopicStudents;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.AdvisorUnassignTopicStudents;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.ConfirmTopicRegistration;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.GetMyRegistrationStatus;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.RegisterTopic;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class TopicRegistrationEndpoints
{
    public static IEndpointRouteBuilder MapTopicRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/thesis/topic-registrations")
            .WithTags("ThesisManagement.TopicRegistrations");

        group.MapPost("/", RegisterTopicAsync)
            .RequireAuthorization()
            .WithSummary("Đăng ký đề tài khóa luận (đơn lẻ hoặc theo nhóm 2 sinh viên)")
            .WithDescription("""
                Sinh viên đăng ký đề tài khóa luận tốt nghiệp đã được công bố trong đợt KLTN đang mở.
                Hỗ trợ cả 2 hình thức: đăng ký đơn lẻ (1 sinh viên) hoặc đăng ký nhóm 2 sinh viên.

                ### Tham số Request Body:
                - `topicId` (Guid, bắt buộc): Định danh duy nhất của đề tài cần đăng ký. Không được để trống hoặc là `00000000-0000-0000-0000-000000000000`.
                - `partnerStudentId` (Guid, tùy chọn): Định danh của sinh viên thứ hai được mời cùng nhóm. Sinh viên này phải là tài khoản sinh viên đang hoạt động (`Active`).
                - `partnerUserCode` (string, tùy chọn): Mã số sinh viên (MSSV) của bạn cùng nhóm nếu không truyền `partnerStudentId`. Hệ thống tự động tra cứu không phân biệt hoa thường.
                *Lưu ý:* Nếu không truyền cả 2 trường đối tác (hoặc trường chuỗi chỉ chứa khoảng trắng), hệ thống xử lý theo hình thức đăng ký đơn lẻ (Solo).

                ### Quy tắc nghiệp vụ & Kiểm soát đồng thời:
                1. **Kiểm tra trạng thái đợt & đề tài:** Đợt KLTN phải đang ở trạng thái `Open` và thời điểm hiện tại nằm trong giai đoạn đăng ký `REGISTRATION_AND_SYLLABUS`. Đề tài phải ở trạng thái `Published`.
                2. **Khóa dòng chống Race Condition (Pessimistic Lock):**
                   - Mở Database Transaction và khóa bản ghi đề tài: `SELECT id, batch_id, advisor_id, title_vi, status, student_number FROM topics.topic WHERE id = @topicId FOR UPDATE`.
                   - Kết hợp cơ chế `SemaphoreSlim` in-process lock để tuần tự hóa triệt để mọi luồng tranh chấp cùng một đề tài.
                3. **Cơ chế quản lý Slot & Gộp nhóm (Slot-based Grouping):**
                   - Đề tài có chỉ tiêu tiếp nhận tối đa 2 sinh viên (`topic.StudentNumber` là 1 hoặc 2).
                   - Nếu đăng ký đơn lẻ vào đề tài có chỉ tiêu 2 sinh viên: sinh viên được xác nhận ngay (`Confirmed`, 1/2 slot). Nếu sau đó có sinh viên khác đăng ký đơn lẻ vào đề tài này, hệ thống tự động gộp cả 2 sinh viên vào cùng nhóm đề tài (`2/2 slot`).
                   - Nếu số lượng slot yêu cầu hoặc số slot đã chiếm vượt quá chỉ tiêu tiếp nhận của đề tài, request bị từ chối với lỗi 409 Conflict (`TopicRegistration.TopicAlreadyTaken`).
                4. **Quy trình gửi Email & Token xác nhận:**
                   - **Đăng ký đơn lẻ:** Thành công và xác nhận ngay lập tức, gửi email thông báo xác nhận cho sinh viên.
                   - **Đăng ký nhóm 2 người:** Người đăng ký (Sinh viên A) ở trạng thái `Confirmed`; bạn cùng nhóm (Sinh viên B) ở trạng thái `Pending` và được cấp mã token xác nhận (băm SHA-256) có thời hạn 24 giờ.
                   - Hệ thống gửi email thông báo cho Sinh viên A và email kèm token xác nhận cho Sinh viên B. Trong 24 giờ này, hệ thống tạm giữ slot cho Sinh viên B.
                5. **Cơ chế Lazy Cleanup:** Ngay trong transaction đăng ký, hệ thống tự động dọn dẹp các lời mời `Pending` quá hạn của đề tài để giải phóng slot tức thời.
                6. **Ràng buộc sinh viên:**
                   - Mỗi sinh viên chỉ được đăng ký tối đa 1 đề tài trong một đợt KLTN (`TopicRegistration.StudentAlreadyRegisteredInBatch`).
                   - Sinh viên không được tự mời chính mình (`TopicRegistration.CannotInviteSelf`).
                   - Sinh viên có từ 3 lần vi phạm quá hạn trở lên (`ViolationCount >= 3`) sẽ bị khóa quyền đăng ký đề tài (`TopicRegistration.StudentRegistrationBlocked`).

                ### Yêu cầu phân quyền (Authorization):
                Yêu cầu Bearer token của tài khoản Sinh viên đang hoạt động (`Role == Student` và `Status == Active`).

                ### Mã phản hồi (Responses):
                - `200 OK`: Đăng ký thành công, trả về `TopicRegistrationResultDto` (chứa `groupId`, `topicId`, `isPendingConfirmation`, `confirmationExpiresAtUtc`, `message`).
                - `400 Bad Request`:
                  - `TopicRegistration.InvalidTopicId`: Mã đề tài rỗng hoặc không hợp lệ.
                  - `TopicRegistration.CannotInviteSelf`: Sinh viên tự mời chính mình.
                  - `TopicRegistration.TopicNotPublished`: Đề tài chưa được công bố.
                  - `TopicRegistration.RegistrationWindowClosed`: Cổng đăng ký đề tài của đợt KLTN đang đóng hoặc ngoài thời gian cho phép.
                  - `TopicRegistration.GroupSizeExceedsTopicQuota`: Số lượng thành viên đăng ký vượt quá chỉ tiêu tiếp nhận của đề tài.
                - `401 Unauthorized`: Người dùng chưa đăng nhập hoặc token hết hạn.
                - `403 Forbidden`:
                  - `TopicRegistration.OnlyStudentsAllowed`: Người dùng không phải là sinh viên đang hoạt động.
                  - `TopicRegistration.StudentRegistrationBlocked`: Tài khoản người đăng ký đã bị khóa quyền đăng ký đề tài do vi phạm 3 lần.
                  - `TopicRegistration.PartnerRegistrationBlocked`: Tài khoản bạn cùng nhóm đã bị khóa quyền đăng ký đề tài.
                - `404 Not Found`:
                  - `TopicRegistration.TopicNotFound`: Không tìm thấy thông tin đề tài trong hệ thống.
                  - `TopicRegistration.PartnerNotFound`: Không tìm thấy thông tin bạn cùng nhóm theo MSSV hoặc Id được cung cấp.
                - `409 Conflict`:
                  - `TopicRegistration.TopicAlreadyTaken`: Đề tài đã có nhóm khác nhận hết chỉ tiêu tiếp nhận.
                  - `TopicRegistration.StudentAlreadyRegisteredInBatch`: Sinh viên (hoặc bạn cùng nhóm) đã đăng ký một đề tài khác trong đợt KLTN này.
                """)
            .Produces<ApiResponse<TopicRegistrationResultDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapPost("/confirm", ConfirmTopicRegistrationAsync)
            .WithSummary("Xác nhận tham gia nhóm đề tài qua mã token")
            .WithDescription("""
                Sinh viên được chỉ định làm thành viên cùng nhóm xác nhận đồng ý tham gia đề tài thông qua mã token bí mật nhận được từ email.

                ### Tham số Request Body:
                - `token` (string, bắt buộc): Chuỗi mã xác nhận nhận được từ email. Hệ thống tự động cắt tỉa khoảng trắng thừa (Trim) và băm SHA-256 để đối chiếu với bản ghi trong cơ sở dữ liệu.

                ### Quy tắc nghiệp vụ & Bảo mật:
                1. **Kiểm tra tính hợp lệ & Tính một lần (Idempotency / Anti-Replay):**
                   - Nếu token không khớp với bất kỳ bản ghi nào: trả về lỗi 400 `TopicRegistration.InvalidConfirmationToken`.
                   - Nếu yêu cầu đã được xác nhận thành công trước đó (ví dụ sinh viên bấm lại link hoặc refresh trình duyệt): trả về lỗi 400 `TopicRegistration.AlreadyConfirmed` để người dùng an tâm rằng mình đã xác nhận thành công.
                2. **Kiểm tra thời hạn (Timeout 24 giờ):**
                   - Token chỉ có hiệu lực trong vòng 24 giờ kể từ thời điểm gửi lời mời.
                   - Nếu quá hạn 24 giờ: trả về lỗi 400 `TopicRegistration.ConfirmationExpired`. Lời mời này sẽ bị thu hồi và ghi nhận vi phạm cho sinh viên không xác nhận.
                3. **Bảo mật phân quyền sở hữu (BOLA - Broken Object Level Authorization):**
                   - Nếu request được gửi kèm Bearer token của một sinh viên khác với sinh viên được mời trong nhóm: hệ thống lập tức từ chối với lỗi 403 Forbidden `TopicRegistration.Forbidden` ("Bạn không có quyền xác nhận yêu cầu của người khác.").
                   - Nếu người dùng xác nhận thông qua link trực tiếp chưa đăng nhập: token bí mật 64 ký tự hex ngẫu nhiên đóng vai trò là chứng thực sở hữu hợp lệ.

                ### Mã phản hồi (Responses):
                - `200 OK`: Xác nhận tham gia nhóm thành công, trả về `ConfirmRegistrationResultDto` (chứa `groupId`, `topicId`, `studentId`, `confirmedAtUtc`, `message`).
                - `400 Bad Request`:
                  - `TopicRegistration.InvalidConfirmationToken`: Token rỗng, khoảng trắng, hoặc không tồn tại.
                  - `TopicRegistration.AlreadyConfirmed`: Yêu cầu tham gia nhóm này đã được xác nhận trước đó.
                  - `TopicRegistration.ConfirmationExpired`: Thời hạn xác nhận 24 giờ đã kết thúc.
                - `403 Forbidden`:
                  - `TopicRegistration.Forbidden`: Tài khoản đang đăng nhập không trùng khớp với sinh viên được mời.
                """)
            .Produces<ApiResponse<ConfirmRegistrationResultDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapGet("/my-status", GetMyStatusAsync)
            .RequireAuthorization()
            .WithSummary("Tra cứu trạng thái đăng ký đề tài và lịch sử vi phạm của sinh viên")
            .WithDescription("""
                Tra cứu chi tiết trạng thái đăng ký đề tài khóa luận hiện tại của sinh viên đang đăng nhập,
                cùng số lần bị cảnh cáo quá hạn và cờ khóa quyền đăng ký.

                ### Thông tin trả về (`StudentRegistrationStatusDto`):
                - `hasRegistered` (bool): `true` nếu sinh viên đã có bản ghi đăng ký đề tài trong hệ thống, `false` nếu chưa đăng ký đề tài nào.
                - `groupId` (Guid?): Định danh của nhóm sinh viên thực hiện đề tài (nếu có).
                - `topicId` (Guid?): Định danh của đề tài đã đăng ký (nếu có).
                - `topicTitleVi` (string?): Tên tiếng Việt của đề tài đã đăng ký.
                - `confirmationStatus` (string?): Trạng thái xác nhận của sinh viên trong nhóm (`Confirmed` - đã xác nhận chính thức, `Pending` - đang chờ bạn xác nhận token).
                - `confirmationExpiresAtUtc` (DateTime?): Thời điểm hết hạn xác nhận token nếu đang ở trạng thái `Pending`.
                - `violationCount` (int): Số lần vi phạm do không xác nhận đề tài đúng hạn 24 giờ (mặc định 0).
                - `isRegistrationBlocked` (bool): `true` nếu tài khoản đã bị khóa quyền đăng ký đề tài (khi `violationCount >= 3`).
                - `blockedAtUtc` (DateTime?): Thời điểm bị khóa quyền đăng ký đề tài (nếu có).

                ### Yêu cầu phân quyền (Authorization):
                Yêu cầu Bearer token hợp lệ của sinh viên (`Role == Student`).

                ### Mã phản hồi (Responses):
                - `200 OK`: Thành công, trả về `StudentRegistrationStatusDto`. Nếu sinh viên chưa từng đăng ký và chưa có vi phạm, trả về thông tin mặc định với `hasRegistered = false`, `violationCount = 0`, `isRegistrationBlocked = false`.
                - `401 Unauthorized`: Người dùng chưa đăng nhập hoặc token đã hết hạn.
                - `403 Forbidden`: Người dùng không có quyền tra cứu (không phải tài khoản sinh viên).
                """)
            .Produces<ApiResponse<StudentRegistrationStatusDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden);

        group.MapPut("/advisor-assign", AdvisorAssignTopicStudentsAsync)
            .RequireAuthorization()
            .WithSummary("GVHD đăng ký / cập nhật nhóm sinh viên cho đề tài của mình")
            .WithDescription("""
                Cho phép Giảng viên hướng dẫn (GVHD) chủ động gán hoặc cập nhật nhóm sinh viên (từ 1 đến tối đa chỉ tiêu tiếp nhận của đề tài)
                vào đề tài do mình phụ trách trong giai đoạn Chuẩn bị và công bố đề tài (`TOPIC_PREPARATION` - tuần 0-1, trước khi mở cổng đăng ký cho sinh viên).

                ### Tham số Request Body:
                - `topicId` (Guid, bắt buộc): Mã định danh duy nhất của đề tài.
                - `studentIds` (array of Guid, tùy chọn): Danh sách ID của các sinh viên cần gán.
                - `studentCodes` (array of string, tùy chọn): Danh sách mã số sinh viên (MSSV) của các sinh viên cần gán.
                *Lưu ý:* Phải cung cấp ít nhất một danh sách (`studentIds` hoặc `studentCodes`). Hệ thống sẽ hợp nhất và loại trừ trùng lặp.

                ### Quy tắc nghiệp vụ & Kiểm soát:
                1. **Phân quyền sở hữu:** Chỉ GVHD phụ trách đề tài (`topic.AdvisorId == currentUserId`) hoặc Quản trị viên (`Admin`) mới có quyền thực hiện.
                2. **Khung thời gian:** Đợt KLTN phải đang ở trạng thái `Open` và thời điểm hiện tại phải nằm trong giai đoạn `TOPIC_PREPARATION` (Stage 1, tuần 0-1).
                3. **Trạng thái đề tài:** Đề tài phải ở trạng thái `Draft`, `Submitted`, `NeedRevision` hoặc `Approved` (chưa công bố `Published` hoặc bị từ chối/hủy).
                4. **Chỉ tiêu tiếp nhận:** Số lượng sinh viên đăng ký phải thỏa mãn `1 <= số lượng <= topic.StudentNumber`.
                5. **Ràng buộc sinh viên:**
                   - Tài khoản sinh viên phải tồn tại, đang hoạt động (`Active`) và có vai trò `Student`.
                   - Sinh viên không bị khóa quyền đăng ký đề tài (`IsRegistrationBlocked == false`).
                   - Sinh viên chưa thuộc bất kỳ nhóm đề tài nào khác trong cùng đợt KLTN.
                6. **Xác nhận tức thì:** Tất cả sinh viên được gán sẽ được xác nhận chính thức ngay lập tức (`Confirmed`), không qua bước chờ token và không chịu phạt vi phạm.
                7. **Gửi email thông báo:** Hệ thống tự động gửi email thông báo cho tất cả sinh viên được gán vào đề tài.

                ### Mã phản hồi (Responses):
                - `200 OK`: Gán / cập nhật nhóm sinh viên thành công, trả về `AdvisorTopicRegistrationResultDto`.
                - `400 Bad Request`: Danh sách sinh viên rỗng, trùng lặp hoặc vượt quá chỉ tiêu tiếp nhận của đề tài.
                - `401 Unauthorized`: Chưa đăng nhập hoặc token hết hạn.
                - `403 Forbidden`: Người dùng không phải GVHD sở hữu đề tài hoặc sinh viên bị khóa quyền đăng ký.
                - `404 Not Found`: Không tìm thấy đề tài hoặc không tìm thấy sinh viên theo mã/ID.
                - `409 Conflict`: Đợt KLTN ngoài giai đoạn `TOPIC_PREPARATION`, đề tài sai trạng thái hoặc sinh viên đã có nhóm khác.
                """)
            .Produces<ApiResponse<AdvisorTopicRegistrationResultDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        group.MapDelete("/advisor-assign/{topicId:guid}", AdvisorUnassignTopicStudentsAsync)
            .RequireAuthorization()
            .WithSummary("GVHD hủy gán nhóm sinh viên khỏi đề tài trong giai đoạn chuẩn bị đề tài")
            .WithDescription("""
                Cho phép GVHD gỡ/hủy toàn bộ nhóm sinh viên đã gán khỏi đề tài của mình khi còn trong giai đoạn `TOPIC_PREPARATION`.
                Sau khi hủy, đề tài trở lại trạng thái trống và các sinh viên được giải phóng để tham gia đề tài khác.

                ### Mã phản hồi (Responses):
                - `200 OK`: Hủy gán nhóm sinh viên thành công.
                - `401 Unauthorized`: Chưa đăng nhập.
                - `403 Forbidden`: Không phải GVHD sở hữu đề tài.
                - `404 Not Found`: Không tìm thấy đề tài hoặc đề tài chưa có nhóm sinh viên nào.
                - `409 Conflict`: Ngoài giai đoạn `TOPIC_PREPARATION` hoặc đề tài sai trạng thái.
                """)
            .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse<object>>(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> AdvisorAssignTopicStudentsAsync(
        [FromBody] AdvisorAssignTopicStudentsRequest? request,
        AdvisorAssignTopicStudentsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new AdvisorAssignTopicStudentsCommand(
                request?.TopicId ?? Guid.Empty,
                request?.StudentIds,
                request?.StudentCodes),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> AdvisorUnassignTopicStudentsAsync(
        [FromRoute] Guid topicId,
        AdvisorUnassignTopicStudentsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new AdvisorUnassignTopicStudentsCommand(topicId),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> RegisterTopicAsync(
        [FromBody] RegisterTopicRequest? request,
        RegisterTopicHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new RegisterTopicCommand(
                request?.TopicId ?? Guid.Empty,
                request?.PartnerStudentId,
                request?.PartnerUserCode),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> ConfirmTopicRegistrationAsync(
        [FromBody] ConfirmTopicRegistrationRequest? request,
        ConfirmTopicRegistrationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new ConfirmTopicRegistrationCommand(request?.Token ?? string.Empty),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetMyStatusAsync(
        GetMyRegistrationStatusHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new GetMyRegistrationStatusQuery(),
            cancellationToken)).ToHttpResult();

    public sealed record RegisterTopicRequest(
        Guid TopicId,
        Guid? PartnerStudentId = null,
        string? PartnerUserCode = null);

    public sealed record ConfirmTopicRegistrationRequest(string Token);

    public sealed record AdvisorAssignTopicStudentsRequest(
        Guid TopicId,
        IReadOnlyCollection<Guid>? StudentIds = null,
        IReadOnlyCollection<string>? StudentCodes = null);
}
