# Danh mục & đăng ký đề tài cho sinh viên

Cập nhật 27/10/2026. Frontend route `/topics/catalog`, menu **Đề tài & đăng ký** hiện cho `Student` và `Admin`. Student được đăng ký/xác nhận; Admin chỉ xem danh mục và chi tiết, không gọi `my-status` hay hiển thị thao tác sinh viên. Tích hợp theo mã nguồn backend trong repository, chưa xác minh phiên bản đang triển khai.

## API đã nối

| API hiện có | Sử dụng |
| --- | --- |
| `GET /api/topics/all-topics` | Danh mục đã công bố, tìm tên tiếng Việt/Anh, lọc đợt và phân trang server. Đã trả `confirmedStudentsCount`, `reservedSlots`, `availableSlots` để hiển thị tình trạng đăng ký. Không gọi tracking của GV hoặc danh sách tài khoản để lấy thêm dữ liệu cho SV. |
| `GET /api/academic-periods/batches` | Tên/ID đợt thật qua hook dùng chung, theo quyền đọc hiện có. |
| `GET /api/academic-periods/batches/{batchId}/timeline` | Kiểm tra trạng thái Open, giai đoạn `REGISTRATION_AND_SYLLABUS`, ngày bắt đầu và deadline; đọc lại mỗi 15 giây khi hộp thoại mở. Backend vẫn kiểm tra lại khi POST. |
| `GET /api/thesis/topic-registrations/my-status` | Hồ sơ hiện tại, trạng thái xác nhận riêng của người đang đăng nhập, hạn lời mời và khóa quyền đăng ký/cảnh báo. Tải lại trước khi gửi đăng ký; cập nhật định kỳ mỗi 30 giây. |
| `POST /api/thesis/topic-registrations/` | Cá nhân gửi `{ "topicId": "..." }`; kèm lời mời gửi thêm `partnerUserCode`. Người đăng ký lấy từ phiên đăng nhập, không cho nhập ID người đăng ký hoặc giả lập xác nhận. |
| `POST /api/thesis/topic-registrations/confirm` | Người được mời nhập mã từ email rồi chủ động xác nhận. Chỉ gửi POST sau khi bấm xác nhận; không tự tiêu thụ token khi mở giao diện/GET. |

## Hành vi frontend

- Dùng PageHeader, ListCardHeader, StatisticsBar, SelectableTableRow, StatusBadge, TablePagination, TableSkeleton, TopicPreview, Dialog, Input và Button chung.
- Bảng và chi tiết dùng chung `TopicCapacityStatus`: hiển thị số SV đã có hiệu lực/chỉ tiêu, số chỗ trống và chỗ chờ xác nhận. Đủ người thì hiển thị Chỉ xem và khóa đăng ký; còn chỗ nhưng không đủ cho 2 SV thì chỉ cho đăng ký cá nhân. API chưa trả số liệu thì hiển thị chưa có số liệu, không lấy chỉ tiêu làm số chỗ trống và không cho gửi đăng ký mù.
- Mở chi tiết sẽ đọc lại trang danh mục; nút đăng ký bị khóa trong lúc cập nhật và dùng số liệu mới khi hoàn tất. Backend vẫn xử lý xung đột nếu chỗ cuối cùng bị người khác lấy giữa GET và POST.
- Hai bước xem lại/xác nhận đăng ký; khóa nút và dùng chốt chống bấm lặp, không tự retry mutation. Rời màn hình/đổi tài khoản hủy request đang chờ; UI riêng của tài khoản được remount theo userId.
- Đăng ký cá nhân có hiệu lực ngay. Nếu mời B, A vẫn có hiệu lực; chỉ B cần xác nhận trong **24 giờ**. Hiển thị hạn từ response, không tính hạn từ thời gian máy người dùng.
- SV được GV chỉ định có trạng thái `Confirmed` thì hiển thị đã đăng ký, không cần POST lại. Giới hạn cùng đợt vẫn do backend thực thi.
- Không tự xác định mọi đăng ký cũ thuộc cùng đợt: `my-status` hiện không trả `batchId`. Nếu chưa biết đợt của đăng ký cũ, gửi request hợp lệ để backend kiểm tra thay vì khóa nhầm toàn bộ các đợt.
- Quyền đăng ký bị khóa vẫn cho xem danh mục/hồ sơ. Lỗi đủ chỉ tiêu, trùng đăng ký, sai MSSV, mời chính mình, ngoài thời gian, token sai/hết hạn/sai chủ sở hữu hiển thị thông báo backend; giữ nội dung nhập khi thất bại và làm mới dữ liệu liên quan.
- Mã xác nhận chỉ lưu trong state lúc nhập, không ghi localStorage, URL hay log. UI xác nhận yêu cầu đăng nhập Student; backend đối chiếu người nhận với tài khoản hiện tại.
- Thông tin gửi lời mời tới B từ response đăng ký chỉ là biên nhận thao tác. Khi tải lại trang, chưa thể theo dõi trạng thái B từ `my-status` của A; không suy ra B đã xác nhận từ trạng thái Confirmed của A.

## Backend còn thiếu để hoàn thiện dữ liệu

Đã bổ sung `advisorName: string | null` vào `/api/topics/all-topics`. Frontend dùng tên này trong bảng và hộp thoại chi tiết/đăng ký; không cần quyền đọc danh sách tài khoản. Không tìm thấy tài khoản giảng viên thì trả null và giao diện dùng ID dự phòng.

| Ưu tiên | Phần cần bổ sung | Ảnh hưởng hiện tại |
| --- | --- | --- |
| P2 | Điều kiện nhận thêm thành viên/đăng ký theo người dùng | Ba số liệu chỗ đã có trong DTO công khai. Điều kiện cá nhân, cửa sổ đăng ký và trạng thái nhóm vẫn phải kiểm tra ở backend; nếu thêm `canRegister` theo SV thì dùng API có xác thực, không trả thông tin cá nhân qua danh mục công khai. |
| P1 | `batchId` và bộ lọc theo đợt cho `my-status`; hoặc danh sách đăng ký của tôi theo đợt | Hiện API lấy bản ghi thành viên mới nhất của SV trên mọi đợt; chưa đủ để tổng hợp mọi hồ sơ/đợt hoặc xác định đăng ký xung đột hoàn toàn ở client. Cần tách trạng thái lịch sử/đã dừng/hết hạn khỏi đăng ký đang có hiệu lực. |
| P1 | Chi tiết đăng ký của tôi: đề tài, GV, đợt, thành viên có hiệu lực, lời mời, hạn và trạng thái từng lời mời | Hiện A không thể xem trạng thái B sau khi reload; SV chỉ định vào đề tài chưa công bố chỉ xem được tên/trạng thái từ my-status, chưa có endpoint đọc nội dung riêng. Endpoint mới phải giới hạn theo SV sở hữu; không mở tracking GV cho tất cả SV. |
| P2 | Chủ động từ chối và gửi lại lời mời | Chưa có API nên chưa hiển thị nút thao tác giả. Gửi lại không gia hạn mốc 24h ban đầu. |
| P2 | Email có liên kết tới màn hình xác nhận | Backend hiện gửi mã token thô; người được mời dùng nút **Xác nhận lời mời** để dán mã. Nếu bổ sung link cần chốt route, vẫn phải bấm xác nhận mới POST. |

Kiểm tra frontend: `npm.cmd run test:student-topics`, `npm.cmd run test:topics`, `npm.cmd run test:advising`, `npm.cmd run build`, `npm.cmd run lint`. Các test không gửi đăng ký hoặc email thật.

## Số liệu chỗ trong API danh mục

Backend đã bổ sung số liệu vào `TopicSummaryDto` và contract đọc liên module. Mỗi request lấy một mốc UTC server để tính:

- `confirmedStudentsCount`: số thành viên `Confirmed`, gồm SV do GV chỉ định và SV đăng ký cá nhân.
- `reservedSlots`: thành viên `Pending` có hạn sau thời điểm đọc; tại `now >= confirmationExpiresAtUtc` không còn tính giữ chỗ, kể cả worker chưa xóa bản ghi.
- `availableSlots = max(0, studentNumber - confirmedStudentsCount - reservedSlots)`; chỉ tiêu cũ thiếu/không hợp lệ thì trả `null`, không tự tạo chỗ trống.
- Không trả danh tính thành viên, token lời mời hoặc ghi chú thẩm định trong response công khai.

| Trạng thái đề tài 2 người | Đã có hiệu lực | Giữ chỗ | Còn trống |
| --- | --- | --- | --- |
| Chưa có SV | 0 | 0 | 2 |
| A đăng ký cá nhân | 1 | 0 | 1 |
| A mời B, lời mời còn hạn | 1 | 1 | 0 |
| B đã xác nhận hoặc GV chỉ định đủ 2 SV | 2 | 0 | 0 |
| Lời mời B đã hết hạn | 1 | 0 | 1 |

Cần triển khai lại **cả backend và frontend** để có các trường mới; cập nhật frontend riêng sẽ hiển thị chưa có số liệu. Không có thay đổi schema database cho phần mở rộng response này.
