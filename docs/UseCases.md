# Use Case UGTM

Nguồn nghiệp vụ: Báo cáo SE310 (Bản Final).docx (tài liệu nghiệp vụ nhóm quản lý ngoài repo), chốt tháng 10/2026. Đây là yêu cầu cần triển khai; trạng thái source xem [Architecture.md](../backend/docs/Architecture/Architecture.md) và [DataModel.md](DataModel.md).

Các đặc tả dưới đây dựa trên mục 2.3.2 của bản final, bổ sung các quy tắc chấm điểm nhóm xác nhận ngày 10/10/2026. Chỉ giữ đặc tả dạng bảng; toàn bộ sơ đồ Use Case được bỏ theo yêu cầu nhóm ngày 10/10/2026. Đăng nhập là điều kiện tiên quyết.

## Đặc tả 12 Use Case chính

### 2.3.2.1. Khởi tạo đợt KLTN và thiết lập mốc thời gian

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Khởi tạo đợt KLTN và thiết lập mốc thời gian |
| Mô tả | Cho phép Quản trị viên hệ thống hoặc Phòng Đào tạo khởi tạo đợt Khóa luận tốt nghiệp theo năm học và học kỳ, thiết lập các mốc thời gian thực hiện trong chu kỳ cố định 21 tuần, đồng thời nhập danh sách sinh viên đủ điều kiện và giảng viên từ tệp Excel. |
| Tác nhân (Actor) | Quản trị viên hệ thống (Admin), Phòng Đào tạo (PĐT). |
| Điều kiện kích hoạt (Trigger) | Admin hoặc PĐT lựa chọn chức năng "Khởi tạo đợt KLTN" trong phân hệ quản lý đợt KLTN. |
| Điều kiện tiên quyết (Pre-condition) | Người thực hiện đã đăng nhập vào hệ thống và được phân quyền khởi tạo đợt KLTN theo vai trò Admin hoặc PĐT. |
| Điều kiện sau (Post-condition) | Thành công: Đợt KLTN mới được ghi nhận theo năm học và học kỳ với chu kỳ 21 tuần; thông tin các giai đoạn, thời hạn và danh sách sinh viên, giảng viên được lưu trong hệ thống. Đợt KLTN có thể tiếp tục bước cấu hình bảng điểm trước khi mở cổng thực hiện.<br>Thất bại: Đợt KLTN chưa được khởi tạo thành công và hệ thống thông báo cho người thực hiện. |
| Luồng hoạt động chính (Main Flow) | 1. Admin hoặc PĐT truy cập phân hệ quản lý đợt KLTN và chọn chức năng "Khởi tạo đợt KLTN".<br>2. Hệ thống hiển thị giao diện nhập thông tin đợt KLTN.<br>3. Người thực hiện nhập năm học, học kỳ và thông tin thời gian của đợt.<br>4. Hệ thống áp dụng chu kỳ thực hiện KLTN cố định 21 tuần.<br>5. Người thực hiện thiết lập thời hạn nộp và xét duyệt cho từng giai đoạn theo quy trình KLTN.<br>6. Người thực hiện tải lên tệp Excel chứa danh sách sinh viên đủ điều kiện và danh sách giảng viên tham gia đợt.<br>7. Hệ thống tiếp nhận thông tin khởi tạo, cấu hình thời hạn và dữ liệu danh sách được nhập.<br>8. Người thực hiện xác nhận lưu thông tin đợt KLTN.<br>9. Hệ thống ghi nhận đợt KLTN cùng các thông tin đã thiết lập và thông báo kết quả khởi tạo. |
| Luồng thay thế (Alternative Flow) | A1 – Admin thực hiện thay PĐT: Tại bước 1, nếu người khởi tạo là Admin, hệ thống cho phép thực hiện cùng quy trình khởi tạo và thiết lập mốc thời gian như PĐT.<br>A2 – Chưa hoàn thành cấu hình điểm: Sau khi khởi tạo đợt, nếu chưa có cấu hình bảng điểm tương ứng, nghiệp vụ tiếp tục ở Use Case "Thiết lập phương thức tính điểm động" trước khi mở cổng đợt KLTN. |

### 2.3.2.2. Thiết lập phương thức tính điểm động

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Thiết lập phương thức tính điểm động |
| Mô tả | Cho phép Phòng Đào tạo hoặc Admin thiết lập và quản lý phương thức tính điểm tổng kết KLTN theo từng đợt, bao gồm danh mục đầu điểm thành phần, tỷ lệ trọng số hoặc hệ số nguyên và các điều kiện tiên quyết. Hệ thống hỗ trợ thay đổi tham số tính điểm trực tiếp trên giao diện mà không cần chỉnh sửa mã nguồn. |
| Tác nhân (Actor) | Phòng Đào tạo (PĐT), Quản trị viên hệ thống (Admin). |
| Điều kiện kích hoạt (Trigger) | PĐT hoặc Admin lựa chọn chức năng "Cấu hình bảng điểm" trong phân hệ quản lý đợt KLTN. |
| Điều kiện tiên quyết (Pre-condition) | Người thực hiện đã đăng nhập và có quyền cấu hình điểm theo RBAC kết hợp Policy-based Authorization. Đợt KLTN theo năm học và học kỳ đã được khởi tạo trên hệ thống. |
| Điều kiện sau (Post-condition) | Thành công: Hệ thống lưu bộ cấu hình tính điểm của đợt KLTN, bao gồm phương thức tính, các đầu điểm thành phần, trọng số/hệ số và điều kiện tiên quyết tương ứng. Bộ cấu hình được sử dụng trong quá trình xét điều kiện bảo vệ và tổng hợp điểm kết thúc đợt.<br>Thất bại: Hệ thống không lưu cấu hình không hợp lệ, đồng thời hiển thị thông báo lỗi để người thực hiện điều chỉnh. |
| Luồng hoạt động chính (Main Flow) | 1. PĐT hoặc Admin truy cập phân hệ quản lý đợt KLTN và chọn chức năng "Cấu hình bảng điểm".<br>2. Hệ thống hiển thị danh sách các đợt KLTN và thông tin cấu hình điểm tương ứng.<br>3. Người thực hiện lựa chọn đợt KLTN cần cấu hình.<br>4. Hệ thống hiển thị giao diện thiết lập phương thức tính điểm tổng kết.<br>5. Người thực hiện lựa chọn phương thức tính theo tỷ lệ trọng số phần trăm (%) hoặc trung bình cộng có hệ số nguyên.<br>6. Người thực hiện thiết lập danh mục các đầu điểm thành phần, bao gồm điểm GVHD (ADVISOR), điểm GVPB (REVIEWER), điểm Hội đồng (COUNCIL) và các đầu điểm tùy chọn khác.<br>7. Người thực hiện gán tỷ lệ trọng số hoặc hệ số nguyên cho từng đầu điểm theo phương thức tính đã chọn.<br>8. Người thực hiện thiết lập các điều kiện tiên quyết cho từng đầu điểm, chẳng hạn ngưỡng điểm GVPB tối thiểu để xét điều kiện ra Hội đồng bảo vệ.<br>9. Người thực hiện xác nhận lưu cấu hình Draft.<br>10. Hệ thống kiểm tra tính hợp lệ của các tham số đã thiết lập.<br>11. Nếu cấu hình hợp lệ, người thực hiện công bố version; hệ thống lưu version Active và giữ lịch sử version cũ. |
| Luồng thay thế (Alternative Flow) | A1 – Tổng trọng số phần trăm không hợp lệ: Tại bước 10, nếu lựa chọn phương thức tính theo tỷ lệ phần trăm nhưng tổng trọng số khác 100%, hệ thống từ chối lưu và yêu cầu điều chỉnh.<br>A2 – Tổng hệ số nguyên không hợp lệ: Tại bước 10, nếu lựa chọn phương thức tính theo hệ số nguyên nhưng tổng hệ số không lớn hơn 0, hệ thống từ chối lưu và hiển thị cảnh báo.<br>A3 – Điều chỉnh cấu hình điểm: PĐT hoặc Admin clone version Active thành Draft mới, nhập lý do thay đổi, kiểm tra và công bố; version Active cũ chuyển Retired, không bị sửa trực tiếp. |

### 2.3.2.3. Đăng ký đề tài và ghép nhóm

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Đăng ký đề tài và ghép nhóm |
| Mô tả | Cho phép sinh viên đủ điều kiện thực hiện KLTN tìm kiếm đề tài đã được Khoa phê duyệt, tạo nhóm tối đa hai thành viên và đăng ký đề tài trực tuyến. Hệ thống sử dụng giao tác cơ sở dữ liệu và cơ chế khóa để kiểm soát chỉ tiêu tiếp nhận, đồng thời tạo phiếu tiếp nhận đăng ký dưới dạng PDF. |
| Tác nhân (Actor) | Sinh viên (tác nhân chính), Giảng viên hướng dẫn (GVHD). |
| Điều kiện kích hoạt (Trigger) | Sinh viên lựa chọn chức năng "Đăng ký đề tài" trên hệ thống. |
| Điều kiện tiên quyết (Pre-condition) | Sinh viên đã đăng nhập và thuộc danh sách đủ điều kiện thực hiện KLTN; cổng đăng ký đang mở; đề tài đã được Khoa phê duyệt, công bố và còn chỉ tiêu tiếp nhận; sinh viên chưa thuộc đề tài được duyệt khác trong cùng học kỳ. |
| Điều kiện sau (Post-condition) | Thành công: Đăng ký đề tài của nhóm được ghi nhận, chỉ tiêu tiếp nhận được cập nhật, phiếu tiếp nhận đăng ký PDF được tạo và GVHD nhận thông báo để xác nhận tiếp nhận nhóm.<br>Thất bại: Đăng ký không được ghi nhận, chỉ tiêu đề tài không bị thay đổi và hệ thống thông báo lý do. |
| Luồng hoạt động chính (Main Flow) | 1. Sinh viên truy cập danh sách đề tài đã được Khoa công bố.<br>2. Hệ thống hiển thị thông tin đề tài, GVHD và chỉ tiêu tiếp nhận còn trống.<br>3. Sinh viên lựa chọn đề tài mong muốn và tạo nhóm thực hiện KLTN với tối đa hai thành viên.<br>4. Sinh viên kiểm tra thông tin nhóm và gửi yêu cầu đăng ký.<br>5. Hệ thống kiểm tra điều kiện tham gia, thời hạn đăng ký, số thành viên và chỉ tiêu còn trống của đề tài.<br>6. Hệ thống thực hiện Database Transaction kết hợp khóa dữ liệu để ghi nhận đăng ký và cập nhật chỉ tiêu tiếp nhận. 7. Hệ thống tạo phiếu tiếp nhận đăng ký đề tài dưới dạng PDF và thông báo kết quả đăng ký thành công.<br>8. Hệ thống gửi thông báo đến GVHD phụ trách đề tài.<br>9. GVHD truy cập danh sách đăng ký và thực hiện xác nhận tiếp nhận nhóm sinh viên. |
| Luồng thay thế (Alternative Flow) | A1 – Sinh viên không đủ điều kiện: Tại bước 5, nếu sinh viên không thuộc danh sách đủ điều kiện hoặc đã có đề tài được duyệt trong học kỳ, hệ thống từ chối đăng ký.<br>A2 – Số thành viên vượt giới hạn: Tại bước 5, nếu nhóm vượt số thành viên cho phép của đề tài hoặc vượt hai sinh viên, hệ thống yêu cầu điều chỉnh thông tin nhóm.<br>A3 – Đề tài hết chỉ tiêu: Tại bước 6, nếu có đăng ký đồng thời khiến chỉ tiêu không còn đủ, hệ thống Rollback giao tác và thông báo đăng ký không thành công.<br>A4 – Hết thời hạn đăng ký: Nếu cổng đăng ký đã đóng, hệ thống không cho phép gửi yêu cầu đăng ký. |

### 2.3.2.4. Nộp và thẩm định đề cương chi tiết

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Nộp và thẩm định đề cương chi tiết |
| Mô tả | Cho phép nhóm sinh viên nộp đề cương chi tiết để Văn phòng Khoa thẩm định tại Cổng kiểm soát 1 (Tuần 2). Kết quả thẩm định quyết định việc nhóm được tiếp tục thực hiện KLTN, phải chỉnh sửa đề cương hoặc dừng KLTN trong học kỳ. |
| Tác nhân (Actor) | Sinh viên (nộp đề cương), Văn phòng Khoa (thẩm định), GVHD (theo dõi đề cương). |
| Điều kiện kích hoạt (Trigger) | Đại diện nhóm chọn chức năng "Nộp đề cương chi tiết" hoặc Văn phòng Khoa chọn chức năng "Thẩm định đề cương". |
| Điều kiện tiên quyết (Pre-condition) | Nhóm đã đăng ký đề tài và được GVHD xác nhận tiếp nhận; tài khoản có quyền truy cập hồ sơ tương ứng; thời hạn nộp hoặc thời hạn thẩm định còn hiệu lực theo cấu hình đợt KLTN. |
| Điều kiện sau (Post-condition) | Đạt: Nhóm chuyển sang trạng thái "Đang thực hiện KLTN" và được mở quyền truy cập các chức năng của giai đoạn tiếp theo.<br>Yêu cầu chỉnh sửa: Nhóm được phép nộp lại đề cương trong thời hạn bổ sung.<br>Không đạt: Nhóm chuyển sang trạng thái "Dừng KLTN" và bị khóa quyền thực hiện các nghiệp vụ KLTN tiếp theo trong học kỳ. |
| Luồng hoạt động chính (Main Flow) | 1. Đại diện nhóm truy cập chức năng nộp đề cương chi tiết. 2. Hệ thống hiển thị thông tin đề tài, nhóm thực hiện và thời hạn nộp.<br>3. Sinh viên tải lên đề cương định dạng PDF, DOC hoặc DOCX và xác nhận nộp.<br>4. Hệ thống kiểm tra định dạng, dung lượng và thời hạn, sau đó lưu hồ sơ đề cương.<br>5. Văn phòng Khoa truy cập danh sách đề cương cần thẩm định.<br>6. Khoa xem nội dung đề cương, nhập nhận xét và đưa ra kết luận.<br>7. Trong trường hợp đề cương "Đạt", hệ thống cập nhật trạng thái nhóm thành "Đang thực hiện KLTN".<br>8. Hệ thống mở quyền truy cập các chức năng thực hiện KLTN tiếp theo và thông báo kết quả thẩm định cho sinh viên. |
| Luồng thay thế (Alternative Flow) | A1 – Yêu cầu chỉnh sửa: Tại bước 6, nếu Khoa chọn "Yêu cầu chỉnh sửa", hệ thống lưu ý kiến phản hồi và mở quyền cho nhóm nộp lại đề cương trước hạn chót bổ sung. Phiên bản cập nhật được chuyển về Khoa để tiếp tục thẩm định.<br>A2 – Không đạt: Tại bước 6, nếu Khoa chọn "Không đạt", hệ thống chuyển trạng thái nhóm sang "Dừng KLTN" và khóa các tính năng thực hiện KLTN trong học kỳ.<br>A3 – Tệp không hợp lệ: Tại bước 4, nếu tệp không đáp ứng yêu cầu định dạng hoặc dung lượng, hệ thống từ chối tiếp nhận và yêu cầu sinh viên chọn lại tệp hợp lệ.<br>A4 – Quá thời hạn: Nếu đã hết hạn nộp và nhóm không có quyền nộp bổ sung hợp lệ, hệ thống không cho phép gửi đề cương. |

### 2.3.2.5. Quản lý tiến độ theo Milestones

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Quản lý tiến độ theo Milestones |
| Mô tả | Cho phép GVHD thiết lập các mốc công việc theo lộ trình thực hiện KLTN, sinh viên nộp sản phẩm tại từng mốc và GVHD đánh giá kết quả. Hệ thống lưu lịch sử nộp, phản hồi, các phiên bản chỉnh sửa và cảnh báo nộp trễ. |
| Tác nhân (Actor) | GVHD, Sinh viên. |
| Điều kiện kích hoạt (Trigger) | GVHD chọn chức năng quản lý Milestones hoặc sinh viên truy cập một Milestone để nộp sản phẩm. |
| Điều kiện tiên quyết (Pre-condition) | Đề cương của nhóm đã được Khoa phê duyệt; nhóm đang ở trạng thái được phép thực hiện KLTN; GVHD và sinh viên thuộc phạm vi đề tài được phân công. |
| Điều kiện sau (Post-condition) | Thành công: Milestone được thiết lập, sản phẩm của sinh viên được lưu, GVHD ghi nhận kết quả đánh giá và sinh viên nhận được phản hồi.<br>Trường hợp cần chỉnh sửa: Hệ thống cho phép nộp lại sản phẩm và lưu lịch sử các phiên bản. |
| Luồng hoạt động chính (Main Flow) | 1. GVHD truy cập phân hệ quản lý tiến độ của nhóm sinh viên.<br>2. GVHD thiết lập các Milestones theo lộ trình, bao gồm nội dung công việc và thời hạn hoàn thành.<br>3. Hệ thống lưu danh sách Milestones và hiển thị trên Dashboard tiến độ của nhóm.<br>4. Sinh viên lựa chọn Milestone cần thực hiện và tải lên báo cáo tiến độ, đường dẫn mã nguồn hoặc sản phẩm thử nghiệm, kèm ghi chú tóm tắt kết quả.<br>5. Hệ thống ghi nhận nội dung và thời điểm nộp sản phẩm.<br>6. GVHD truy cập hồ sơ Milestone, xem sản phẩm và nhập nhận xét.<br>7. GVHD chọn kết luận "Đạt" hoặc "Cần chỉnh sửa".<br>8. Hệ thống lưu kết quả đánh giá, lịch sử phản hồi và gửi thông báo in-app, email đến sinh viên. |
| Luồng thay thế (Alternative Flow) | A1 – Cần chỉnh sửa sản phẩm: Tại bước 7, nếu GVHD đánh giá "Cần chỉnh sửa", hệ thống cho phép sinh viên tải lên phiên bản sản phẩm mới (v2, v3,...). Các phiên bản và lần đánh giá trước đó vẫn được lưu.<br>A2 – Nộp trễ Milestone: Tại bước 5, nếu thời điểm nộp vượt hạn chót, hệ thống ghi nhận trạng thái trễ và hiển thị cảnh báo trên Dashboard để GVHD theo dõi, làm căn cứ đánh giá tiến độ.<br>A3 – Đề cương chưa đạt: Nếu nhóm chưa vượt qua Cổng kiểm soát 1, hệ thống không mở chức năng thực hiện Milestones. |

### 2.3.2.6. Nộp và đánh giá báo cáo giữa kỳ

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Nộp và đánh giá báo cáo giữa kỳ |
| Mô tả | Cho phép mọi thành viên nhóm nộp báo cáo giữa kỳ; Văn phòng Khoa hoặc Admin thẩm định tại Cổng kiểm soát 2. Kết luận `Approved`, `NeedRevision` hoặc `Rejected` quyết định nhóm tiếp tục, nộp lại hoặc dừng KLTN. |
| Tác nhân (Actor) | Sinh viên, Văn phòng Khoa, Admin, Phòng Đào tạo. |
| Điều kiện kích hoạt (Trigger) | Sinh viên chọn chức năng "Nộp báo cáo giữa kỳ" hoặc Khoa truy cập phiên bản đã nộp để thẩm định. |
| Điều kiện tiên quyết (Pre-condition) | Nhóm đã vượt qua Cổng kiểm soát 1 và đang thực hiện KLTN; sinh viên có quyền nộp trong deadline cấu hình; Khoa/Admin có quyền thẩm định. |
| Điều kiện sau (Post-condition) | `Approved`: nhóm tiếp tục thực hiện KLTN.<br>`NeedRevision`: nhóm được nộp lại phiên bản mới đến deadline chốt cuối cùng.<br>`Rejected`, hoặc chưa được `Approved` khi Cổng đóng: nhóm chuyển `Stopped` và bị khóa nghiệp vụ tiếp theo. |
| Luồng hoạt động chính (Main Flow) | 1. Sinh viên theo dõi deadline Cổng 2 trên Dashboard.<br>2. Bất kỳ thành viên nhóm nào nộp tệp báo cáo giữa kỳ.<br>3. Hệ thống kiểm tra tệp, deadline và lưu phiên bản private.<br>4. Văn phòng Khoa truy cập phiên bản đang chờ thẩm định, nhập nhận xét và chọn `Approved`.<br>5. Hệ thống lưu kết luận, lịch sử phiên bản/thẩm định và giữ nhóm ở trạng thái thực hiện KLTN.<br>6. Khoa tổng hợp kết quả cần thiết để PĐT theo dõi. |
| Luồng thay thế (Alternative Flow) | A1 – `NeedRevision`: Khoa nhập feedback; hệ thống mở quyền nộp v2, v3... trong deadline bổ sung, không ghi đè lịch sử.<br>A2 – `Rejected`: Khoa nhập feedback; hệ thống chuyển nhóm sang `Stopped` và khóa giai đoạn sau.<br>A3 – Cổng đóng: worker idempotent dừng các nhóm có báo cáo chưa `Approved` sau deadline chốt cuối cùng.<br>A4 – Tệp hoặc thời hạn không hợp lệ: hệ thống từ chối nộp, không tạo phiên bản hay object public. |

### 2.3.2.7. Xử lý đơn đổi tên đề tài liên thông ba cấp

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Xử lý đơn đổi tên đề tài liên thông ba cấp |
| Mô tả | Cho phép sinh viên đề nghị thay đổi tên đề tài KLTN trực tuyến trong giai đoạn Tuần 3–10. Đơn được phê duyệt tuần tự qua ba cấp: GVHD, Văn phòng Khoa và Phòng Đào tạo. Tên đề tài chỉ được thay đổi chính thức sau khi PĐT phê duyệt. |
| Tác nhân (Actor) | Sinh viên, GVHD, Văn phòng Khoa, Phòng Đào tạo. |
| Điều kiện kích hoạt (Trigger) | Sinh viên chọn chức năng "Tạo đơn xin đổi tên đề tài" trên hệ thống. |
| Điều kiện tiên quyết (Pre-condition) | Sinh viên đã đăng nhập và thuộc nhóm đang thực hiện KLTN; đề tài đã được ghi nhận trên hệ thống; thời điểm tạo đơn nằm trong giai đoạn Tuần 3–10; người phê duyệt có quyền tương ứng tại từng cấp. |
| Điều kiện sau (Post-condition) | Thành công: Đơn được cả ba cấp phê duyệt, hệ thống cập nhật tên đề tài mới, chuyển trạng thái đơn sang "Hoàn tất", ghi nhận thay đổi vào AuditLog và mở quyền nộp lại đề cương cập nhật.<br>Bị từ chối: Đơn dừng xử lý, tên đề tài cũ được giữ nguyên và sinh viên nhận được lý do từ chối. |
| Luồng hoạt động chính (Main Flow) | 1. Sinh viên truy cập chức năng đổi tên đề tài trong giai đoạn cho phép.<br>2. Hệ thống hiển thị biểu mẫu gồm thông tin đề tài hiện tại, tên đề tài mới bằng tiếng Việt và tiếng Anh, lý do điều chỉnh.<br>3. Sinh viên hoàn thành biểu mẫu và gửi đơn.<br>4. Hệ thống chuyển đơn đến GVHD để xét duyệt cấp 1.<br>5. GVHD xem xét và chọn "Đồng ý"; hệ thống chuyển trạng thái đơn sang "Chờ Khoa duyệt".<br>6. Văn phòng Khoa thẩm định và thực hiện "Ký duyệt"; hệ thống chuyển trạng thái sang "Chờ PĐT phê duyệt".<br>7. Phòng Đào tạo kiểm tra hồ sơ liên thông và chọn "Phê duyệt chính thức".<br>8. Hệ thống cập nhật tên đề tài mới vào cơ sở dữ liệu, lưu lịch sử thay đổi vào AuditLog và chuyển trạng thái đơn sang "Hoàn tất".<br>9. Hệ thống mở quyền cho sinh viên nộp lại đề cương chi tiết cập nhật.<br>10. Hệ thống gửi email và thông báo in-app kết quả đến sinh viên, GVHD và Văn phòng Khoa. |
| Luồng thay thế (Alternative Flow) | A1 – GVHD từ chối: Tại bước 5, GVHD nhập lý do từ chối. Hệ thống dừng luồng duyệt, giữ nguyên tên đề tài và thông báo lý do cho sinh viên.<br>A2 – Khoa từ chối: Tại bước 6, Văn phòng Khoa nhập lý do từ chối. Đơn không được chuyển đến PĐT và tên đề tài không thay đổi.<br>A3 – PĐT từ chối: Tại bước 7, Phòng Đào tạo nhập lý do từ chối. Hệ thống kết thúc quy trình mà không cập nhật tên đề tài.<br>A4 – Ngoài thời gian quy định: Nếu sinh viên tạo đơn sau Tuần 10, hệ thống khóa chức năng tạo đơn đổi tên đề tài. |

### 2.3.2.8. Chấm điểm GVHD, GVPB và xét điều kiện bảo vệ

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Chấm điểm GVHD, GVPB và xét điều kiện bảo vệ |
| Mô tả | Cho phép sinh viên nộp báo cáo cuối kỳ; GVHD hiện được phân công hoặc Admin nhận xét, chấm điểm và quyết định bàn giao sang phản biện. Hệ thống chỉ mở điều kiện phân công GVPB khi quyết định đồng ý đã khóa; bước GVPB và xét Hội đồng thuộc workflow tiếp theo. |
| Tác nhân (Actor) | Sinh viên, GVHD, Admin, GVPB, Văn phòng Khoa. |
| Điều kiện kích hoạt (Trigger) | Sinh viên nộp báo cáo cuối kỳ hoặc GVHD truy cập báo cáo để bắt đầu đánh giá. |
| Điều kiện tiên quyết (Pre-condition) | Nhóm đã được Khoa hoặc Admin `Approved` tại Cổng kiểm soát 2; đang trong giai đoạn Tuần 11–18; báo cáo và hồ sơ thuộc phạm vi đề tài được phân công; cấu hình điều kiện ra bảo vệ đã được thiết lập cho đợt KLTN. |
| Điều kiện sau (Post-condition) | Đồng ý: phiếu GVHD gắn đúng báo cáo/file đã khóa, điểm và quyết định khóa, nhóm `ReadyForReviewerAssignment`; contract phân công GVPB trả điều kiện hợp lệ. Chưa đồng ý: nhóm vẫn `FinalReportSubmitted`, bản báo cáo vẫn khóa, chưa thể phân công GVPB và GVHD/Admin có thể xét lại. |
| Luồng hoạt động chính (Main Flow) | 1. Sinh viên tải lên báo cáo cuối kỳ toàn văn định dạng PDF/DOC/DOCX ≤20 MiB (20,971,520 byte) trong FINAL_EVALUATION của đợt Open (mặc định Tuần 11–18). Thành viên Student đã Confirmed được nộp lại trong hạn khi chưa bắt đầu chấm; giữ toàn bộ phiên bản.<br>2. GVHD hiện được phân công hoặc Admin đọc một phiên bản, bấm Bắt đầu chấm với `expectedVersionNo`; hệ thống chuyển `UnderEvaluation` và khóa nộp lại.<br>3. GVHD/Admin lưu điểm 0–10 và nhận xét cho đúng phiên bản/file đã khóa. Server làm tròn điểm hai chữ số thập phân và trả `evaluationVersion`; trước khi duyệt, lần sửa sau phải gửi lại phiên bản này.<br>4. GVHD/Admin chọn đồng ý chuyển phản biện. Trong một transaction, hệ thống khóa phiếu, chuyển nhóm sang `ReadyForReviewerAssignment` và ghi audit actor thực tế.<br>5. Workflow phân công GVPB kiểm tra lại contract eligibility với báo cáo, phiên bản, file và checksum; chỉ khi hợp lệ mới được thực hiện các bước GVPB tiếp theo. |
| Luồng thay thế (Alternative Flow) | A1 – Chưa đồng ý chuyển phản biện: tại bước 4, GVHD/Admin phải nhập lý do; hệ thống giữ nhóm `FinalReportSubmitted`, bản báo cáo khóa và chặn phân công GVPB. GVHD/Admin có thể sửa phiếu rồi xét lại; không tự dừng nhóm, mở nộp lại hoặc gán điểm 0.<br>A2 – Phiên bản đã cũ hoặc có ghi đồng thời: server trả xung đột; client tải lại phiếu/báo cáo trước khi gửi ý định mới.<br>A3 – Điểm hoặc nhận xét không hợp lệ: server từ chối điểm ngoài 0–10, nhận xét rỗng hoặc vượt giới hạn; không lưu một phần dữ liệu.<br>A4 – Sau khi đồng ý: tất cả sửa điểm, nhận xét và quyết định mới đều bị chặn; SCRUM-103 không có thu hồi quyết định.<br>A5 – Bảo mật: Student, Faculty chỉ có quyền xem báo cáo theo contract SCRUM-102; họ và GVHD cũ không xem được phiếu GVHD. |

### 2.3.2.9. Thành lập Hội đồng và tổ chức bảo vệ

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Thành lập Hội đồng và tổ chức bảo vệ |
| Mô tả | Cho phép Văn phòng Khoa lập dự thảo danh sách Hội đồng bảo vệ, phân công Chủ tịch, Thư ký, Ủy viên, phòng thi, ca thi và đề tài đủ điều kiện. Phòng Đào tạo thẩm định, phê duyệt và tải lên Quyết định thành lập Hội đồng; hệ thống công bố lịch bảo vệ để các bên liên quan tra cứu. |
| Tác nhân (Actor) | Văn phòng Khoa, Phòng Đào tạo, Sinh viên, GVHD. |
| Điều kiện kích hoạt (Trigger) | Văn phòng Khoa chọn chức năng "Lập dự thảo Hội đồng bảo vệ". |
| Điều kiện tiên quyết (Pre-condition) | Đang trong giai đoạn tổ chức bảo vệ theo kế hoạch đợt KLTN; danh sách đề tài đủ điều kiện bảo vệ đã được xác định; người thực hiện đã đăng nhập và có quyền phân công hoặc phê duyệt Hội đồng tương ứng. |
| Điều kiện sau (Post-condition) | Thành công: Hội đồng được phê duyệt, Quyết định thành lập Hội đồng được lưu trên hệ thống và lịch bảo vệ được công bố. Sinh viên và GVHD được phép tra cứu lịch liên quan đến đề tài của mình.<br>Thất bại: Dự thảo chưa được phê duyệt hoặc chưa đáp ứng ràng buộc phân công; lịch chưa được công bố chính thức. |
| Luồng hoạt động chính (Main Flow) | 1. Văn phòng Khoa truy cập chức năng thành lập Hội đồng bảo vệ.<br>2. Hệ thống hiển thị danh sách đề tài đủ điều kiện bảo vệ.<br>3. Khoa lập dự thảo Hội đồng, phân công Chủ tịch, Thư ký và các Ủy viên.<br>4. Khoa xác định phòng thi, ca thi và gán các đề tài đủ điều kiện vào Hội đồng tương ứng.<br>5. Hệ thống kiểm tra ràng buộc GVHD không được tham gia Hội đồng chấm chính đề tài mình hướng dẫn.<br>6. Khoa hoàn tất dự thảo và chuyển dữ liệu đến Phòng Đào tạo.<br>7. Phòng Đào tạo truy cập dự thảo, thẩm định thông tin và tải lên Quyết định thành lập Hội đồng được phê duyệt.<br>8. Hệ thống ghi nhận quyết định phê duyệt và công bố lịch bảo vệ.<br>9. Sinh viên và GVHD truy cập hệ thống để tra cứu thời gian, phòng thi, ca thi và thành viên Hội đồng. |
| Luồng thay thế (Alternative Flow) | A1 – GVHD trùng thành viên Hội đồng: Tại bước 5, nếu phát hiện GVHD được phân công vào Hội đồng chấm đề tài mình hướng dẫn, hệ thống từ chối phân công đó và yêu cầu Khoa điều chỉnh.<br>A2 – Đề tài chưa đủ điều kiện bảo vệ: Tại bước 4, nếu đề tài chưa được xác nhận đủ điều kiện, hệ thống không cho phép đưa vào danh sách xếp lịch Hội đồng.<br>A3 – Dự thảo chưa được PĐT phê duyệt: Sau bước 7, nếu chưa có phê duyệt chính thức, hệ thống chưa công bố lịch bảo vệ của dự thảo đó. |

### 2.3.2.10. Chấm điểm Hội đồng, xử lý điểm sai biệt và khóa biên bản

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Chấm điểm Hội đồng, xử lý điểm sai biệt và khóa biên bản |
| Mô tả | Cho phép từng thành viên Hội đồng chấm điểm và nhập ý kiến đánh giá tại buổi bảo vệ. Hệ thống hỗ trợ cảnh báo điểm sai biệt để Hội đồng thảo luận; Thư ký ghi biên bản và thực hiện khóa điểm. Khi có khiếu nại hợp lệ, Lãnh đạo Khoa có quyền cấp phép mở khóa đặc biệt để giảng viên Hội đồng chỉnh sửa điểm do chính mình nhập. |
| Tác nhân (Actor) | Hội đồng bảo vệ, bao gồm Chủ tịch, Thư ký và Ủy viên; Lãnh đạo Khoa trong trường hợp cấp quyền mở khóa đặc biệt. |
| Điều kiện kích hoạt (Trigger) | Thành viên Hội đồng truy cập chức năng chấm điểm cho đề tài tại buổi bảo vệ, hoặc Thư ký chọn chức năng "Khóa biên bản điểm" sau khi hoàn tất đánh giá. |
| Điều kiện tiên quyết (Pre-condition) | Hội đồng đã được Phòng Đào tạo phê duyệt; đề tài được phân công vào Hội đồng và đủ điều kiện bảo vệ; thành viên đã đăng nhập bằng tài khoản cá nhân và có quyền chấm đề tài tương ứng. |
| Điều kiện sau (Post-condition) | Thành công: Điểm và ý kiến đánh giá của các thành viên được lưu; điểm trung bình Hội đồng được tính từ các điểm hợp lệ; biên bản bảo vệ được Thư ký hoàn thành và khóa bằng xác thực mật khẩu/OTP. Điểm Hội đồng chuyển sang trạng thái chỉ đọc.<br>Trường hợp đặc biệt: Các điểm không được Hội đồng công nhận được đánh dấu để loại khỏi tính toán; việc cấp quyền mở khóa và chỉnh sửa điểm hợp lệ được ghi vào AuditLog. |
| Luồng hoạt động chính (Main Flow) | 1. Tại buổi bảo vệ, từng thành viên Hội đồng đăng nhập bằng tài khoản cá nhân; riêng GVPB kiêm ủy viên chỉ tham dự, không nhập điểm Hội đồng cho cùng đề tài.<br>2. Hệ thống hiển thị danh sách đề tài được phân công và bảng tiêu chí chấm điểm.<br>3. Từng thành viên nhập điểm thành phần theo thang 0.0–10.0 và ý kiến đánh giá.<br>4. Hệ thống ghi nhận điểm theo tài khoản của từng thành viên và tính điểm trung bình Hội đồng từ các điểm hợp lệ.<br>5. Hệ thống kiểm tra, hiển thị cảnh báo khi phát hiện điểm chấm sai biệt cần thảo luận.<br>6. Chủ tịch Hội đồng tổ chức trao đổi, thống nhất kết quả đánh giá trước khi khóa biên bản.<br>7. Thư ký Hội đồng ghi biên bản bảo vệ điện tử, bao gồm kết quả đánh giá và các yêu cầu chỉnh sửa đối với báo cáo.<br>8. Thư ký lựa chọn chức năng "Khóa biên bản điểm".<br>9. Hệ thống yêu cầu Thư ký xác thực bằng mật khẩu hoặc OTP.<br>10. Khi xác thực thành công, hệ thống khóa biên bản và chuyển điểm Hội đồng sang trạng thái chỉ đọc. |
| Luồng thay thế (Alternative Flow) | A1 – Phát hiện điểm sai biệt: Tại bước 5, khi điểm của thành viên Hội đồng, CBHD hoặc CBPB chênh lệch từ 2 điểm trở lên so với điểm tổng kết theo quy định áp dụng, hệ thống phát cảnh báo để Hội đồng xem xét. Nếu sau thảo luận kết quả vẫn không thay đổi và Hội đồng kết luận không công nhận điểm sai biệt, Chủ tịch xác nhận kết luận; Thư ký ghi vào biên bản và đánh dấu điểm tương ứng bằng IsIgnoredInCalculation = True. Hệ thống loại cả điểm và hệ số đã được xác nhận không công nhận; tính lại bằng tổng (điểm × hệ số) hợp lệ chia tổng hệ số hợp lệ còn lại.<br>A2 – Khiếu nại sau khi khóa biên bản: Nếu có khiếu nại hợp lệ, Lãnh đạo Khoa sử dụng chức năng "Cấp quyền mở khóa đặc biệt" và nhập lý do giải trình. Hệ thống ghi nhận thao tác vào AuditLog.<br>A3 – Chỉnh sửa điểm sau khi được cấp quyền: Sau khi có quyền mở khóa hợp lệ, chỉ giảng viên Hội đồng đã nhập điểm được sửa điểm do chính mình nhập; hệ thống từ chối yêu cầu sửa điểm của thành viên khác và ghi nhận mọi thay đổi vào AuditLog.<br>A4 – Xác thực khóa biên bản không thành công: Tại bước 9, nếu Thư ký không xác thực thành công, hệ thống không thực hiện thao tác khóa.<br>A5 – Thành viên tự sửa điểm đã khóa: Nếu chưa có quyền mở khóa hợp lệ, hệ thống từ chối mọi thao tác chỉnh sửa điểm đã khóa. |

### 2.3.2.11. Nộp và xác nhận hồ sơ lưu chiểu

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Nộp và xác nhận hồ sơ lưu chiểu |
| Mô tả | Cho phép sinh viên hoàn thiện và nộp hồ sơ lưu chiểu sau bảo vệ; GVHD kiểm tra, xác nhận bản chỉnh sửa; Văn phòng Khoa nghiệm thu hồ sơ. Trạng thái hoàn thành lưu chiểu là điều kiện bắt buộc trước khi sinh viên được tra cứu kết quả KLTN chính thức. |
| Tác nhân (Actor) | Sinh viên, GVHD, Văn phòng Khoa. |
| Điều kiện kích hoạt (Trigger) | Sau khi nhận biên bản góp ý của Hội đồng, sinh viên truy cập chức năng "Nộp hồ sơ lưu chiểu". |
| Điều kiện tiên quyết (Pre-condition) | Nhóm đã hoàn thành buổi bảo vệ; có nội dung góp ý chỉnh sửa từ Hội đồng; đang trong giai đoạn lưu chiểu Tuần 20–21 và chưa vượt quá thời hạn nộp theo quy định. |
| Điều kiện sau (Post-condition) | Thành công: Hồ sơ lưu chiểu được GVHD xác nhận đạt yêu cầu chỉnh sửa, Khoa xác nhận hoàn thành và hệ thống cập nhật trạng thái lưu chiểu thành công.<br>Chưa đạt: Hồ sơ chưa được xác nhận hoàn thành, sinh viên được yêu cầu chỉnh sửa và nộp lại; quyền tra cứu điểm chính thức vẫn bị khóa. |
| Luồng hoạt động chính (Main Flow) | 1. Sau buổi bảo vệ, sinh viên truy cập biên bản góp ý do Thư ký Hội đồng ghi nhận.<br>2. Sinh viên chỉnh sửa báo cáo toàn văn theo các yêu cầu của Hội đồng.<br>3. Sinh viên truy cập chức năng nộp lưu chiểu và tải lên hồ sơ hoàn thiện gồm tệp PDF, DOCX, đoạn tóm tắt nội dung và từ khóa tiếng Việt/Anh.<br>4. Hệ thống ghi nhận hồ sơ và thời điểm nộp.<br>5. GVHD truy cập báo cáo đã chỉnh sửa, kiểm tra nội dung và xác nhận đạt yêu cầu chỉnh sửa.<br>6. Văn phòng Khoa truy cập hồ sơ lưu chiểu đã được GVHD xác nhận.<br>7. Khoa kiểm tra tính hợp lệ của hồ sơ và chọn "Xác nhận hoàn thành nộp lưu chiểu".<br>8. Hệ thống cập nhật trạng thái hoàn thành lưu chiểu và ghi nhận việc đáp ứng điều kiện tiên quyết để chuyển sang bước trả điểm chính thức. |
| Luồng thay thế (Alternative Flow) | A1 – Hồ sơ lưu chiểu chưa đạt: Tại bước 7, nếu hồ sơ không hợp lệ, Khoa từ chối xác nhận và gửi yêu cầu chỉnh sửa. Sinh viên tiếp tục hoàn thiện, nộp lại trước hạn chót tổng hợp.<br>A2 – Chưa được GVHD xác nhận: Nếu GVHD chưa xác nhận bản chỉnh sửa đạt yêu cầu, quy trình chưa hoàn tất bước kiểm tra của GVHD và hồ sơ chưa đủ điều kiện hoàn thành lưu chiểu.<br>A3 – Chưa hoàn thành lưu chiểu: Nếu chưa có xác nhận hoàn thành từ Khoa, hệ thống tiếp tục khóa quyền tra cứu điểm chính thức của nhóm, dù các đầu điểm đã được chấm đầy đủ. |

### 2.3.2.12. Tổng hợp điểm và công bố kết quả KLTN

| Thuộc tính | Mô tả |
| --- | --- |
| Tên Use Case | Tổng hợp điểm và công bố kết quả KLTN |
| Mô tả | Cho phép Văn phòng Khoa kích hoạt tổng hợp điểm KLTN theo bộ trọng số/hệ số động của từng đợt, rà soát bảng điểm dự thảo và công bố kết quả chính thức. Hệ thống kiểm tra điều kiện hoàn thành lưu chiểu, loại trừ những điểm không được công nhận và hỗ trợ Khoa, Phòng Đào tạo xuất báo cáo phục vụ xét tốt nghiệp. |
| Tác nhân (Actor) | Văn phòng Khoa (tác nhân chính), Phòng Đào tạo, Sinh viên (tra cứu sau công bố). |
| Điều kiện kích hoạt (Trigger) | Văn phòng Khoa lựa chọn chức năng "Tổng hợp điểm đợt KLTN" trong phân hệ Điểm và Báo cáo. |
| Điều kiện tiên quyết (Pre-condition) | Đợt KLTN đã có cấu hình tính điểm hợp lệ; dữ liệu chấm điểm đã được ghi nhận; người thực hiện có quyền tổng hợp và công bố kết quả. Đối với mỗi đề tài được tổng hợp chính thức, hệ thống phải kiểm tra đầy đủ các đầu điểm bắt buộc và trạng thái hoàn thành lưu chiểu. |
| Điều kiện sau (Post-condition) | Thành công: Hệ thống tính điểm tổng kết theo cấu hình động, tạo bảng điểm dự thảo để Khoa rà soát; khi Khoa công bố, kết quả được phát hành chính thức, sinh viên đủ điều kiện được xem điểm chi tiết và hệ thống gửi thông báo. Khoa và PĐT có thể xuất bảng điểm tổng hợp Excel/PDF.<br>Thất bại: Đề tài chưa đáp ứng điều kiện không được tổng hợp chính thức hoặc công bố điểm cho sinh viên. |
| Luồng hoạt động chính (Main Flow) | 1. Văn phòng Khoa truy cập phân hệ Điểm và chọn "Tổng hợp điểm đợt KLTN".<br>2. Hệ thống truy xuất bộ cấu hình phương thức tính điểm, trọng số/hệ số và các đầu điểm thành phần của đợt.<br>3. Hệ thống kiểm tra dữ liệu điểm và trạng thái hoàn thành lưu chiểu của từng đề tài.<br>4. Hệ thống xác định các điểm hợp lệ, bao gồm điểm GVHD, GVPB, điểm Hội đồng và các đầu điểm tùy chọn theo cấu hình.<br>5. Hệ thống áp dụng phương thức tính theo tỷ lệ phần trăm hoặc hệ số nguyên, đồng thời xét trường hợp GVPB có hoặc không tham gia Hội đồng theo quy tắc tính điểm của đợt.<br>6. Hệ thống tạo bảng điểm tổng kết ở trạng thái "Dự thảo".<br>7. Văn phòng Khoa kiểm tra các đầu điểm và kết quả tính toán.<br>8. Khoa chọn chức năng "Công bố kết quả".<br>9. Hệ thống chuyển trạng thái kết quả sang chính thức, mở quyền tra cứu điểm chi tiết cho sinh viên đủ điều kiện và gửi email thông báo kết quả.<br>10. Khoa hoặc Phòng Đào tạo lựa chọn chức năng xuất báo cáo tổng hợp theo định dạng Excel (.xlsx) hoặc PDF phục vụ xét tốt nghiệp. |
| Luồng thay thế (Alternative Flow) | A1 – Thiếu đầu điểm bắt buộc: Tại bước 3, nếu đề tài chưa có đầy đủ các đầu điểm theo cấu hình, hệ thống không thực hiện tổng hợp chính thức cho đề tài và hiển thị thông tin thiếu.<br>A2 – Chưa hoàn thành lưu chiểu: Tại bước 3, nếu hồ sơ lưu chiểu chưa được Khoa xác nhận hoàn thành, hệ thống chưa cho phép công bố điểm chính thức cho nhóm.<br>A3 – Có điểm không được Hội đồng công nhận: Tại bước 4, nếu một điểm đã được Hội đồng kết luận không công nhận và Thư ký đánh dấu IsIgnoredInCalculation = True, hệ thống loại trừ điểm đó khỏi phép tính theo quy chế và tính lại kết quả dựa trên các điểm hợp lệ.<br>A4 – Chưa công bố kết quả: Nếu Khoa chưa thực hiện bước 8, bảng điểm vẫn ở trạng thái "Dự thảo" và sinh viên chưa được tra cứu điểm chính thức.<br>A5 – GVPB tham gia Hội đồng: Tại bước 5, hệ thống áp dụng quy tắc tính tương ứng đã được xác định cho trường hợp GVPB đồng thời là thành viên Hội đồng, không mặc định sử dụng cùng cách tính với trường hợp GVPB không tham gia Hội đồng. |


Quy tắc công thức chấm điểm được quản lý tập trung tại [Requirements.md](Requirements.md). Đây là đặc tả nghiệp vụ cần triển khai, không xác nhận API đã tồn tại. Hai công thức chia 6/chia 7, việc GVPB kiêm ủy viên không chấm Hội đồng và loại cả điểm/hệ số khi điểm không được công nhận đã được nhóm xác nhận ngày 10/10/2026.

## Phân công GVPB — phạm vi triển khai SCRUM-104

| Thuộc tính | Nội dung |
| --- | --- |
| Tác nhân | Faculty/Khoa và AcademicAffairs/PĐT có permission read/manage tương ứng; Admin được bỏ qua permission. Phạm vi toàn hệ thống. GVPB có tài khoản Active và Lecturer. |
| Tiên quyết tạo | Đợt Open; phiếu GVHD đã đồng ý/khóa và khớp bản InitialFinal đang UnderEvaluation; nhóm ReadyForReviewerAssignment. Không phụ thuộc cửa sổ nộp bài của sinh viên. |
| Luồng chính | 1. Người quản lý tra cứu nhóm đủ điều kiện và ứng viên Active/Lecturer khác GVHD hiện tại.<br>2. Gửi POST với group/reviewer, expectedAdvisorEvaluationId, expectedReportVersionNo và UUID Idempotency-Key.<br>3. Server khóa các điều kiện trong transaction, kiểm tra lại, tạo một phân công active, snapshot và audit/receipt cùng commit.<br>4. GVPB đọc nhiệm vụ của chính mình và xin URL tải đúng file đã duyệt; server kiểm tra lại quyền ngay khi cấp URL. |
| Hủy/đổi người | Quản lý gửi DELETE JSON gồm expectedAssignmentVersion và lý do trim 1–1000 Unicode rune, UUID key. Nếu chưa có bất kỳ phiếu GVPB nào, hủy mềm và audit người/thời điểm thực tế; không yêu cầu đợt Open. Phân công lại dùng POST/key mới, giữ dòng cũ; có thể chọn lại cùng người. |
| Ngoại lệ | Trùng active, binding/GVHD đã đổi, phiên bản cũ hoặc key trùng nhưng khác ý định trả 409; thiếu quyền 403. Mọi phiếu GVPB, kể cả điểm 0/ẩn/bị loại khỏi tính điểm, đều chặn hủy. Retry cùng ý định/key trả receipt gốc, không ghi thêm audit hoặc kích hoạt lại dòng đã hủy. |
| Hậu điều kiện | Nhóm vẫn ReadyForReviewerAssignment, báo cáo vẫn khóa; giữ lịch sử và audit. Hủy chặn cấp URL mới; URL đã cấp còn dùng tới TTL 5 phút. Quyền Student/GVHD/FacultyGate/Admin độc lập theo SCRUM-102 vẫn áp dụng. |
| Giới hạn | Quyền GVPB không mở lịch sử báo cáo hoặc phiếu/điểm GVHD. Nhập điểm SCRUM-105, phản biện thứ hai, thông báo và tổng hợp điểm thuộc task khác. Xem [contract](ApiContract/ReviewerAssignmentApiContract.md) và [bằng chứng nghiệm thu](ApiContract/ReviewerAssignmentVerification.md). |

## Chấm GVPB — phạm vi triển khai SCRUM-105 CP2

| Nội dung | Mô tả |
| --- | --- |
| Tác nhân | GVPB Active/Lecturer của assignment hoặc Admin nhập/chốt; Admin ghi actor thật. Faculty/AcademicAffairs có role + thesis.reviewer-evaluations.read và Admin đọc để điều phối. |
| Tiền điều kiện ghi | Đợt Open; assignment active/nonlegacy; group/report/phiếu duyệt GVHD/file/version/checksum còn khớp; reviewer hợp lệ, khác GVHD hiện tại. Không dùng FINAL_EVALUATION/hạn nộp bài hoặc cấu hình ngưỡng để chặn ghi. |
| Luồng chính | 1. GET phiếu riêng tư theo assignmentId; chưa có trả 404, không tạo nháp.<br>2. Gửi POST SaveDraft cùng version/key; nội dung thiếu được null, toàn bộ form thay thế nháp cũ.<br>3. GET/lưu tiếp nháp bằng evaluationVersion mới.<br>4. POST Submit đủ điểm/góp ý/kết luận; lần tạo 201, sửa/chốt nháp 200.<br>5. Server khóa phiếu, đặt visibility cho comment và commit phiếu/receipt/audit nguyên tử. |
| Ngoại lệ | Version cũ, key khác ý định, binding sai hoặc đợt đóng trả 409; thiếu quyền 403; JSON/score/comment/nội dung Submit sai trả 400. Đã chốt Approved/Rejected đều không sửa hoặc thu hồi. Retry key/ý định cũ trả DTO/status gốc theo quyền hiện tại, không audit đôi hoặc mở lại nháp. |
| Hậu điều kiện và giới hạn | Mọi phiếu chặn hủy assignment. Đọc riêng tư theo quyền vẫn được sau đóng đợt/chuyển giai đoạn; không mở điểm GVHD hoặc tải file khác. CP2 chưa có GET feedback/gate hoặc xếp Hội đồng; chưa công bố điểm cho sinh viên. Xem [contract và bằng chứng CP2](ApiContract/ReviewerEvaluationApiContract.md#14-cp2--lưu-nháp-chốt-và-đọc-phiếu-riêng-tư). |
