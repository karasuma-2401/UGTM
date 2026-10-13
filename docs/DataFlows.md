# Luồng dữ liệu UGTM

Nguồn nghiệp vụ: Báo cáo SE310 (Bản Final).docx (tài liệu nghiệp vụ nhóm quản lý ngoài repo), chốt tháng 10/2026. Đây là yêu cầu cần triển khai; trạng thái source xem [Architecture.md](../backend/docs/Architecture/Architecture.md) và [DataModel.md](DataModel.md).

Năm tiến trình SĐ1–SĐ5 tương ứng mục 2.4 của Word. Đây là mô tả luồng dữ liệu (DFD), khác với [biểu đồ tuần tự](../backend/docs/Architecture/Sequences.md). Quy ước D2 của báo cáo được giữ nguyên: không phát sinh từ thiết bị nhập độc lập.


## 2.4.1 Đăng ký đề tài và ghép nhóm


### 2.4.1.1. Biểu mẫu

BM1: PHIẾU ĐĂNG KÝ ĐỀ TÀI


| Mã đề tài | Tên đề tài |
| --- | --- |
| Giảng viên hướng dẫn | Số lượng sinh viên tiếp nhận tối đa |
| Sinh viên đại diện: MSSV, họ tên, email, SĐT | Sinh viên thứ hai (nếu có): MSSV, họ tên, email |
| Thời gian đăng ký | Trạng thái: Chờ GVHD xác nhận |


### 2.4.1.2. Quy định

QĐ1.1: Nhóm thực hiện KLTN có tối đa 02 sinh viên và không được vượt quá chỉ tiêu tiếp nhận của đề tài.

QĐ1.2: Sinh viên phải thuộc danh sách đủ điều kiện và không được đăng ký đồng thời nhiều đề tài trong cùng đợt KLTN.

QĐ1.3: Đề tài phải được Khoa công bố, còn chỉ tiêu và đang trong thời hạn đăng ký.

QĐ1.4: Sử dụng Database Transaction kết hợp khóa dữ liệu để ngăn vượt chỉ tiêu khi có nhiều nhóm đăng ký đồng thời.

Các luồng dữ liệu

D1: Mã đề tài lựa chọn, danh sách thành viên nhóm và yêu cầu đăng ký của sinh viên.

D2: Không phát sinh luồng từ thiết bị nhập độc lập trong mô hình này (giữ quy ước của bảng DFD gốc).

D3: Thông tin đề tài đã công bố, chỉ tiêu trống, danh sách sinh viên/nhóm đã đăng ký và cấu hình thời hạn.

D4: Hồ sơ đăng ký mới, nhóm sinh viên, chỉ tiêu đề tài sau cập nhật và trạng thái “Chờ GVHD xác nhận”.

D5: Phiếu tiếp nhận đăng ký đề tài được xuất dưới định dạng PDF.

D6: Thông báo kết quả đăng ký thành công hoặc thất bại cho sinh viên.


### 2.4.1.4. Thuật toán xử lý

B1: Tiếp nhận mã đề tài và thành viên nhóm từ sinh viên (D1).

B2: Bắt đầu giao tác CSDL, khóa bản ghi đề tài và đọc thông tin kiểm tra (D3).

B3: Kiểm tra thời hạn đăng ký, trạng thái công bố và chỉ tiêu còn trống của đề tài.

B4: Kiểm tra từng thành viên đủ điều kiện, chưa đăng ký đề tài khác trong đợt và không vượt giới hạn nhóm/đề tài.

B5: Nếu vi phạm bất kỳ điều kiện nào, Rollback giao tác; trả thông báo lỗi (D6) và kết thúc.

B6: Nếu hợp lệ, ghi nhận nhóm và yêu cầu đăng ký, cập nhật chỉ tiêu tiếp nhận, đặt trạng thái “Chờ GVHD xác nhận” (D4).

B7: Commit giao tác; tạo phiếu tiếp nhận đăng ký PDF (D5).

B8: Hiển thị kết quả đăng ký (D6) và gửi thông báo nghiệp vụ cho GVHD để xác nhận tiếp nhận nhóm; thông báo đến GVHD không phải luồng riêng trên hình SĐ1 gốc.

B9: Kết thúc xử lý yêu cầu; thao tác GVHD xác nhận tiếp nhận nhóm thực hiện tiếp theo theo luồng 1.4.2.

Lưu ý nghiệp vụ: Phiếu tiếp nhận đăng ký không đồng nghĩa GVHD đã xác nhận tiếp nhận nhóm.


## 2.4.2 Nộp và thẩm định đề cương chi tiết


### 2.4.2.1. Biểu mẫu

BM2: PHIẾU THẨM ĐỊNH ĐỀ CƯƠNG


| Mã nhóm / Mã đề tài | Tên đề tài |
| --- | --- |
| Tệp đề cương: tên, dung lượng, phiên bản | Thời điểm nộp |
| Kết luận: Đạt / Yêu cầu chỉnh sửa / Không đạt | Ý kiến nhận xét |
| Người thẩm định | Ngày, giờ thẩm định |


### 2.4.2.2. Quy định

QĐ2.1: Đại diện nhóm nộp đề cương dưới dạng .pdf, .doc hoặc .docx, dung lượng không vượt quá 20 MB.

QĐ2.2: Đề cương được nộp trước hạn chót Tuần 2; nộp lại chỉ thực hiện trong thời hạn bổ sung khi Khoa yêu cầu chỉnh sửa.

QĐ2.3: Khoa thẩm định theo ba kết luận: Đạt, Yêu cầu chỉnh sửa, Không đạt.

QĐ2.4: Đạt: chuyển nhóm sang “Đang thực hiện KLTN”; Yêu cầu chỉnh sửa: cho phép nộp lại; Không đạt: chuyển “Dừng KLTN” và khóa nghiệp vụ tiếp theo của đợt.

Các luồng dữ liệu

D1: Mã nhóm/đề tài, kết luận thẩm định, nhận xét, người và thời điểm thẩm định từ Văn phòng Khoa.

D2: Không phát sinh luồng từ thiết bị nhập độc lập trong mô hình này.

D3: Thông tin đề tài, hồ sơ đề cương, phiên bản nộp, trạng thái nhóm và thời hạn thẩm định.

D4: Kết quả thẩm định, lịch sử nhận xét, trạng thái mới của nhóm hoặc cờ cho phép nộp lại đề cương.

D5: Phiếu thẩm định đề cương hiển thị/in xuất khi có nhu cầu.

D6: Thông báo kết quả thẩm định và nội dung phản hồi liên quan đến sinh viên, GVHD theo quy trình.


### 2.4.2.4. Thuật toán xử lý

B1: Sinh viên đã nộp đề cương; hệ thống lưu tệp, thời điểm và phiên bản theo quyền, thời hạn hợp lệ.

B2: Văn phòng Khoa gửi thông tin thẩm định cho đề cương được chọn (D1).

B3: Hệ thống đọc hồ sơ, phiên bản đề cương và trạng thái nhóm (D3).

B4: Kiểm tra đề cương thuộc giai đoạn thẩm định hợp lệ và người thực hiện có quyền Khoa.

B5: Nếu kết luận “Đạt”, cập nhật trạng thái nhóm thành “Đang thực hiện KLTN”, mở các chức năng giai đoạn tiếp theo.

B6: Nếu kết luận “Yêu cầu chỉnh sửa”, lưu ý kiến phản hồi và mở quyền nộp bản mới trước hạn chót bổ sung.

B7: Nếu kết luận “Không đạt”, cập nhật trạng thái “Dừng KLTN” và khóa nghiệp vụ KLTN tiếp theo của nhóm trong học kỳ.

B8: Lưu kết quả/lịch sử thẩm định và trạng thái thay đổi (D4), xuất phiếu khi cần (D5).

B9: Phản hồi kết luận và gửi thông báo đến sinh viên, GVHD (D6).

Lưu ý nghiệp vụ: Bước tải tệp của sinh viên là dữ liệu đã được lưu tại kho đề cương; sơ đồ SĐ2 tập trung vào tiến trình Khoa lập phiếu thẩm định


## 2.4.3. Đơn xin đổi tên đề tài online liên thông 3 bên


### 2.4.3.1. Biểu mẫu

BM3: ĐƠN XIN ĐỔI TÊN ĐỀ TÀI


| Mã đề tài | Tên đề tài hiện tại |
| --- | --- |
| Tên mới đề xuất (tiếng Việt) | Tên mới đề xuất (tiếng Anh) |
| Lý do điều chỉnh | Thời gian tạo đơn |
| Cấp 1 – GVHD: Đồng ý / Từ chối; ý kiến | Cấp 2 – Khoa: Duyệt / Từ chối; ý kiến |
| Cấp 3 – PĐT: Phê duyệt chính thức / Từ chối | Trạng thái và lịch sử xử lý |


### 2.4.3.2. Quy định

QĐ3.1: Sinh viên chỉ được khởi tạo đơn đổi tên đề tài trong giai đoạn Tuần 3 đến Tuần 10.

QĐ3.2: Thứ tự xét duyệt bắt buộc: GVHD (cấp 1) → Văn phòng Khoa (cấp 2) → Phòng Đào tạo (cấp 3).

QĐ3.3: Nếu bất kỳ cấp nào từ chối, phải nhập lý do; dừng quy trình và giữ nguyên tên đề tài hiện tại.

QĐ3.4: Tên đề tài chỉ được cập nhật sau khi PĐT phê duyệt chính thức; lúc đó mới mở quyền nộp lại đề cương cập nhật.

QĐ3.5: Kết quả liên thông và lịch sử cập nhật tên đề tài được hệ thống ghi vết; chỉ Admin có quyền xem AuditLog.

Các luồng dữ liệu

D1: Thông tin đề nghị đổi tên hoặc quyết định duyệt/từ chối của tác nhân đúng cấp, kèm lý do/ý kiến.

D2: Không phát sinh dữ liệu từ thiết bị nhập độc lập trong mô hình này.

D3: Tên đề tài gốc, trạng thái đơn, lịch sử xét duyệt và quyền của người thao tác.

D4: Trạng thái mới của đơn; quyết định từng cấp; tên đề tài mới sau PĐT duyệt; nhật ký hệ thống.

D5: Đơn đổi tên điện tử và thông tin các cấp xét duyệt, không mặc định có chữ ký số.

D6: Thông báo kết quả và trạng thái xử lý đơn đổi tên đến sinh viên, GVHD và Khoa theo giai đoạn.


### 2.4.3.4. Thuật toán xử lý

B1: Sinh viên gửi đơn với tên mới tiếng Việt/Anh và lý do trong Tuần 3–10 (D1).

B2: Hệ thống xác thực quyền, thời hạn và lưu đơn ở trạng thái chờ GVHD xét duyệt.

B3: GVHD xem xét; nếu đồng ý, hệ thống chuyển đơn sang “Chờ Khoa duyệt”.

B4: Khoa thẩm định; nếu ký duyệt, hệ thống chuyển đơn sang “Chờ PĐT phê duyệt”.

B5: PĐT xem xét và ra quyết định phê duyệt chính thức hoặc từ chối.

B6: Nếu bị từ chối tại bất kỳ cấp nào, ghi nhận lý do, dừng luồng xử lý, giữ nguyên tên đề tài và gửi thông báo (D6).

B7: Nếu PĐT phê duyệt, hệ thống cập nhật tên đề tài mới, trạng thái “Hoàn tất”, mở quyền nộp lại đề cương.

B8: Ghi nhận lịch sử và thao tác cập nhật vào kho dữ liệu/AuditLog (D4); tạo bản đơn điện tử (D5).

B9: Gửi kết quả xử lý đến các bên liên quan theo quy trình (D6).

Lưu ý nghiệp vụ: Quy trình phê duyệt trực tuyến không đồng nghĩa bắt buộc sử dụng chữ ký số; tài liệu luồng chốt yêu cầu phê duyệt chính thức trên hệ thống.


## 2.4.4 Chấm điểm Hội đồng và Khóa biên bản điểm


### 2.4.4.1. Biểu mẫu

BM4: BIÊN BẢN CHẤM ĐIỂM HỘI ĐỒNG


| Hội đồng số, phòng bảo vệ | Ngày, ca bảo vệ |
| --- | --- |
| Tên đề tài | Danh sách sinh viên/nhóm |
| Chủ tịch: điểm, nhận xét | Thư ký: điểm, nhận xét |
| Ủy viên: điểm, nhận xét | Điểm trung bình Hội đồng hợp lệ |
| Nội dung góp ý/chỉnh sửa | Kết luận điểm sai biệt (nếu có) |
| Trạng thái: Chưa khóa / Đã khóa | Thư ký khóa, thời điểm khóa |


### 2.4.4.2. Quy định

QĐ4.1: Mỗi thành viên Hội đồng chỉ được nhập điểm cho đề tài được phân công và theo tài khoản cá nhân.

QĐ4.2: Điểm thành viên thuộc thang 0.0–10.0; phiếu gốc quy định thể hiện đến một chữ số thập phân.

QĐ4.3: Chỉ Thư ký Hội đồng được ghi biên bản và bấm “Khóa biên bản điểm” sau khi xác thực mật khẩu/OTP; biên bản đã khóa trở thành chỉ đọc.

QĐ4.4: Khi có khiếu nại hợp lệ, chỉ Lãnh đạo Khoa được cấp quyền mở khóa đặc biệt kèm lý do; sau đó chỉ giảng viên Hội đồng đã nhập điểm được sửa điểm của chính mình.

QĐ4.5: Điểm sai biệt từ 2 điểm trở lên của thành viên Hội đồng, CBHD hoặc CBPB so với điểm tổng kết phải được Hội đồng thảo luận; chỉ điểm bị Hội đồng kết luận không công nhận mới được đánh dấu loại trừ.

QĐ4.6: Chủ tịch điều phối xử lý điểm sai biệt; Thư ký ghi nhận kết luận và đánh dấu `IsIgnoredInCalculation = True` theo biên bản. Chỉ Admin xem AuditLog.

Các luồng dữ liệu

D1: Điểm/nhận xét của thành viên, biên bản và lệnh khóa của Thư ký; cấp quyền mở khóa kèm lý do từ Lãnh đạo Khoa.

D2: Không phát sinh dữ liệu từ thiết bị nhập độc lập trong mô hình này.

D3: Danh sách phân công Hội đồng và đề tài, điểm hiện có, kết luận thảo luận, trạng thái khóa và quyền mở khóa.

D4: Điểm từng thành viên, điểm trung bình hợp lệ, cờ loại trừ theo kết luận, trạng thái khóa, lý do mở khóa và nhật ký thao tác.

D5: Biên bản bảo vệ điện tử và bảng điểm Hội đồng được lập từ dữ liệu hợp lệ.

D6: Thông báo lưu điểm, cảnh báo chấm lệch hoặc thông báo kết quả khóa/mở khóa cho đúng người có quyền.


### 2.4.4.4. Thuật toán xử lý

B1: Thành viên Hội đồng được phân công truy cập đề tài, nhập điểm 0.0–10.0 và nhận xét cá nhân (D1).

B2: Hệ thống kiểm tra phân công, quyền chấm và trạng thái khóa của biên bản (D3).

B3: Nếu hợp lệ và biên bản chưa khóa, lưu điểm từng tài khoản và tính điểm trung bình từ các điểm Hội đồng hợp lệ. Riêng GVPB kiêm ủy viên không nhập thêm điểm Hội đồng của cùng đề tài, chỉ tham dự theo quy tắc nhóm đã chốt.

B4: Nếu xuất hiện điểm sai biệt cần xem xét, cảnh báo cho Chủ tịch tổ chức thảo luận; không tự động loại điểm chỉ vì có cảnh báo.

B5: Theo kết luận không công nhận của Hội đồng đối với điểm sai biệt theo quy định, Chủ tịch xác nhận; Thư ký ghi vào biên bản và đánh dấu điểm bị loại trừ.

B6: Thư ký hoàn thành nội dung biên bản, xác thực mật khẩu/OTP và yêu cầu khóa điểm (D1).

B7: Nếu xác thực hợp lệ, hệ thống đặt trạng thái khóa, lưu thời điểm/người thực hiện, chuyển điểm sang chỉ đọc (D4).

B8: Nếu có khiếu nại hợp lệ, chỉ Lãnh đạo Khoa cấp quyền mở khóa đặc biệt với lý do; hệ thống ghi AuditLog.

B9: Sau khi cấp quyền hợp lệ, chỉ giảng viên Hội đồng đã nhập điểm được sửa điểm chính mình; hệ thống ghi lại các thay đổi vào AuditLog (D4).

B10: Xuất biên bản (D5) và phản hồi trạng thái thao tác cho người có quyền (D6).

Lưu ý nghiệp vụ: Chưa xác định thuật toán tham chiếu điểm tổng kết dùng để phát hiện sai biệt, thời hạn quyền sửa hay thủ tục khóa lại sau chỉnh sửa; giữ mở để nhóm thống nhất sau.


## Tổng hợp điểm động và công bố kết quả


### 2.4.5.1 Biểu mẫu

BM5: BẢNG TỔNG HỢP ĐIỂM KHÓA LUẬN TỐT NGHIỆP


| Đợt KLTN / Năm học / Học kỳ | Mã đề tài, tên đề tài |
| --- | --- |
| Nhóm sinh viên | Trạng thái hoàn thành lưu chiểu |
| Điểm GVHD (ADVISOR) | Điểm GVPB (REVIEWER) |
| Điểm Hội đồng (COUNCIL) | Các đầu điểm tùy chọn theo cấu hình |
| Phương thức: % hoặc hệ số nguyên | Điểm tổng kết KLTN |
| Trạng thái: Dự thảo / Đã công bố | Thời điểm công bố |


### 2.4.5.2. Quy định

QĐ5.1: Chỉ tổng hợp chính thức khi đề tài có đủ các đầu điểm bắt buộc theo cấu hình và được Khoa xác nhận hoàn thành lưu chiểu.

QĐ5.2: Phương thức tính điểm tổng kết do cấu hình đợt quyết định: tỷ lệ phần trăm hoặc hệ số nguyên; không cố định ba đầu điểm.

QĐ5.3: Nhóm đã xác nhận công thức chia 6 khi GVPB là ủy viên, chia 7 khi GVPB ngoài Hội đồng; GVPB kiêm ủy viên chỉ tham dự, không chấm điểm Hội đồng. Bộ hệ số và ví dụ xem Requirements.md.

QĐ5.4: Chỉ điểm đã được Hội đồng kết luận không công nhận mới được gắn `IsIgnoredInCalculation = True` và loại khỏi phép tính theo quy chế.

QĐ5.5: Bảng điểm ở trạng thái Dự thảo trước khi Khoa công bố; sinh viên chỉ xem điểm chính thức sau công bố và hoàn thành lưu chiểu.

QĐ5.6: Khoa và Phòng Đào tạo được xuất báo cáo tổng hợp Excel/PDF; riêng quyền công bố kết quả thuộc Văn phòng Khoa.

Các luồng dữ liệu

D1: Lệnh tổng hợp điểm, lệnh công bố từ Văn phòng Khoa hoặc yêu cầu xuất báo cáo của Khoa/PĐT.

D2: Không phát sinh dữ liệu từ thiết bị nhập độc lập trong mô hình này.

D3: Các đầu điểm theo cấu hình, hồ sơ xác nhận lưu chiểu, trọng số/hệ số đợt, trạng thái điểm và cờ loại trừ hợp lệ.

D4: Điểm tổng kết được tính theo cấu hình động; trạng thái Dự thảo/Đã công bố và thời điểm công bố.

D5: Bảng điểm tổng hợp KLTN xuất định dạng Excel (.xlsx) hoặc PDF.

D6: Kết quả tổng hợp/công bố điểm, thông báo công bố cho sinh viên đủ điều kiện và phản hồi nghiệp vụ cho Khoa/PĐT.


### 2.4.5.4. Thuật toán xử lý

B1: Khoa gửi lệnh “Tổng hợp điểm đợt KLTN” (D1).

B2: Hệ thống đọc cấu hình phương thức tính, các đầu điểm thành phần, điều kiện tiên quyết và trạng thái lưu chiểu (D3).

B3: Kiểm tra từng đề tài có đủ đầu điểm bắt buộc theo cấu hình và đã được Khoa xác nhận hoàn thành lưu chiểu hay chưa.

B4: Đối với đề tài hợp lệ, xác định các điểm được công nhận; loại khỏi tử số và mẫu số cả điểm và hệ số đã được Hội đồng kết luận không công nhận theo cờ `IsIgnoredInCalculation` hợp lệ.

B5: Áp dụng công thức động của đợt theo phương thức phần trăm/hệ số và quy tắc GVPB tham gia hoặc không tham gia Hội đồng.

B6: Ghi điểm tổng kết và tạo bảng điểm ở trạng thái “Dự thảo” để Văn phòng Khoa rà soát (D4).

B7: Khoa xem xét dự thảo và thực hiện lệnh “Công bố kết quả”.

B8: Hệ thống cập nhật trạng thái “Đã công bố”, thời điểm công bố; mở quyền xem điểm cho nhóm đã hoàn thành lưu chiểu (D4).

B9: Hiển thị bảng điểm chi tiết và gửi email thông báo sau công bố cho sinh viên đủ điều kiện; các thông báo đến sinh viên không được tách thành luồng riêng trên sơ đồ SĐ5 gốc.

B10: Khoa/PĐT chọn xuất báo cáo tổng hợp Excel hoặc PDF (D5); hệ thống trả kết quả xử lý (D6).

Biểu đồ tuần tự cho các Use Case chính

Các xác nhận chấm điểm của nhóm ngày 10/10/2026 đã được bổ sung vào QĐ5.3 và các bước loại điểm; nguồn quy tắc tập trung tại [Requirements.md](Requirements.md).
