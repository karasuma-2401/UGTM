# Đề tài hướng dẫn — contract tích hợp

Cập nhật 13/11/2026. Giữ luồng đăng ký tự động: đăng ký cá nhân/chỉ định có hiệu lực ngay, thành viên được mời xác nhận trong 24 giờ theo hạn server. Không có bước giảng viên phê duyệt tiếp nhận riêng.

## Hai tab

- **Đề tài của tôi**: đề xuất, sửa, gửi Khoa thẩm định và theo dõi công bố.
- **Nhóm & tiến độ**: tất cả nhóm thuộc đề tài của GV, với phạm vi đang thực hiện/hoàn thành/đã dừng/tất cả, lọc tiến độ và lời mời.
- Cùng route `/topics/advising`; tab nhóm dùng `?tab=groups`. Link cũ `tab=registrations` mở phạm vi all, `tab=advising` mở active. Các tab giữ batchId khi chuyển; bộ lọc đã áp dụng lưu trong URL.
- Tab đề tài dùng chọn một dòng. Không có nút giả lập xử lý hàng loạt.

## API đọc

| Endpoint | Hành vi |
|---|---|
| GET /api/topics/my-topics | Danh sách đề tài phân trang server, FE truyền advisorId của phiên hiện tại để tài khoản nhiều vai trò vẫn giữ phạm vi cá nhân. |
| GET /api/topics/my-statistics?batchId=... | Một request, trả `{total, submitted, needRevision}`; luôn lấy UserId phiên đăng nhập, chỉ Lecturer/Admin. Bỏ batchId là tất cả đợt. Không tải nhóm/thành viên. |
| GET /api/thesis/advising/groups | Danh sách nhóm phân trang và thống kê; chỉ Lecturer/Admin, luôn lấy UserId của phiên, không nhận advisorId tùy ý. Bao gồm nhóm Completed/Stopped; không dùng contract tính chỉ tiêu để đọc lịch sử. |
| GET /api/thesis/groups/{groupId}/detail | Đọc đúng nhóm, kể cả đã dừng. Giữ quyền đọc hiện tại của endpoint. Bổ sung mô tả/yêu cầu/chỉ tiêu/ngày tạo và thời điểm xác nhận/hạn lời mời. |
| GET /api/academic-periods/batches | Danh sách đợt cho bộ lọc, cần academic.batches.read. Thiếu quyền không chặn danh sách đề tài/nhóm; tên đợt nhóm đã có trong DTO nhóm. |
| GET /api/academic-periods/batches/{id}/timeline | Lịch chuẩn bị đề tài khi chọn đợt trên tab đề tài. |

### Query danh sách nhóm

- `batchId`: UUID tùy chọn; bỏ trống lấy mọi đợt của GV.
- `keyword`: tối đa 200 ký tự; tìm tất cả từ theo tên đề tài Việt/Anh, tên/MSSV/email sinh viên và UUID nhóm. Không phân biệt hoa/thường, dấu tiếng Việt. %, _ và backslash được hiểu theo nghĩa chữ.
- `scope`: active (mặc định), all, completed, stopped. Active là mọi trạng thái nhóm ngoài Completed/Stopped, không phải trạng thái đề tài hoặc xác nhận của từng thành viên.
- `status`: tên chính xác StudentGroupStatus, bao gồm ReadyForReviewerAssignment.
- `confirmation`: bỏ trống, pending hoặc expired.
- `sortBy`: createdAt (mặc định), title, studentCount (số đã Confirmed); `sortOrder`: desc (mặc định) hoặc asc. Có groupId làm khóa phụ ổn định.
- `pageNumber` tối thiểu 1; `pageSize` trong 1–100. Lọc → sắp xếp → phân trang ở server; không tải toàn bộ trang rồi xử lý ở FE.
- Giá trị lọc không hợp lệ trả 400; chưa đăng nhập 401; sai vai trò 403.

Response: `{page: PagedResult<AdvisingGroupDto>, statistics: AdvisingStatisticsDto}`.

Mỗi nhóm có groupId/topicId, titleVi/titleEn, batchId/batchName, status, createdAt, confirmedStudentsCount, pendingConfirmationsCount, expiredInvitationsCount và students. Students gồm studentId, userCode, fullName, confirmationStatus, joinedAt, confirmedAtUtc, confirmationExpiresAtUtc; không có token/hash.

Trong endpoint danh sách mới, confirmationStatus có thể là Expired (suy diễn từ thời hạn UTC server, không phải giá trị enum được ghi thêm vào DB). Pending chưa có deadline là lời mời đang gửi; Pending có deadline còn hạn là chờ xác nhận.

Thống kê có total/active/completed/stopped theo GV và batchId, độc lập keyword/status/confirmation/phân trang; activeConfirmedStudents/pendingInvitations/expiredInvitations chỉ tính các nhóm active. Không dùng tổng số member làm số Confirmed.

### Chi tiết nhóm và tiến độ

Endpoint detail giữ confirmationStatus gốc Confirmed/Pending để tương thích; thêm invitationExpired tính từ giờ server cùng joinedAt, confirmedAtUtc, confirmationExpiresAtUtc, invitedByStudentId. UI không suy ra người mời/nguồn chỉ định từ thứ tự thành viên hoặc joinedAt. Không có dữ liệu nguồn chỉ định đầy đủ thì không hiển thị nguồn giả.

Sau khi đọc và được phép xem nhóm, từng khối tiến độ tải độc lập:

- GET /api/thesis/groups/{id}/submissions/Syllabus
- GET /api/thesis/groups/{id}/submissions/Midterm
- GET /api/thesis/groups/{id}/final-report
- GET /api/thesis/groups/{id}/progress-milestones
- GET /api/thesis/progress-milestones/{id}/report cho từng mốc

Một khối lỗi không làm mất các khối còn lại; mỗi khối có thử lại. Danh sách tải lại mỗi 60 giây; chi tiết thành viên mỗi 30 giây. Chưa có mã nhóm nghiệp vụ: nhãn dùng UUID rút gọn ổn định, có UUID đầy đủ trong chi tiết, không gán Nhóm 01 theo STT.

## Tạo/sửa và chỉ định sinh viên

POST /api/topics, PUT /api/topics/{id}, POST /api/topics/{id}/submit giữ nguyên. Form chỉ gửi studentCodes khi tạo/chủ động sửa chỉ định; bỏ toàn bộ gửi clearAssignedStudents=true. Không gọi thêm mutation chỉ định khi chỉ sửa nội dung. Nhân bản giữ nội dung, không giữ sinh viên hoặc đợt.

Chỉ định chỉ áp dụng trong TOPIC_PREPARATION. Giới hạn cũ vẫn còn: muốn tăng chỉ tiêu 1 → 2 và chỉ định thêm SV thì phải lưu chỉ tiêu trước, sau đó lưu chỉ định. Không thay đổi giao tác mutation trong lần chỉnh UI/read model này.

## Giới hạn và tương thích

- Contract theo dõi đề tài cũ vẫn chỉ trả nhóm hiện tại không Stopped và có số chỉ tiêu theo cách cũ. Giữ nguyên để không đổi luồng đăng ký; tab nhóm mới không sử dụng contract này.
- Bộ chọn nhóm của phân hệ milestones vẫn dùng getTopics cũ; chưa thay đổi nghiệp vụ phân hệ đó.
- Soạn nội dung liên hệ Khoa chỉ sao chép, chưa gửi yêu cầu. Không có nút giả lập hủy/từ chối/gửi lại lời mời.
- Không thêm schema, migration, mã nhóm hoặc bước tiếp nhận của GV; không cần backfill dữ liệu.
- FE mới cần BE có hai endpoint đọc mới. Các endpoint cũ vẫn hoạt động.
