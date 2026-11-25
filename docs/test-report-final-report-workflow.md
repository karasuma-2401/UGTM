# Báo cáo kiểm thử luồng sau nộp Báo cáo cuối kỳ

- Ngày lập: 25/11/2026 (Asia/Bangkok).
- Phạm vi: Báo cáo cuối kỳ → GVHD → phân công/chấm phản biện → Hội đồng → lưu chiểu → công bố điểm.
- Môi trường mục tiêu: local, frontend `http://localhost:5173`, backend `http://localhost:5287`, API Reference `/scalar`.
- Người kiểm thử: ................................
- Commit/build được kiểm thử: ................................
- Đợt KLTN / batchId: ................................
- Ngày giờ thực hiện: ................................

## 1. Cách dùng báo cáo

1. Chuẩn bị tài khoản và dữ liệu theo mục 3.
2. Chạy luồng thành công trước: FR-01 → FR-02 → AD-01 → AD-02 → AD-03 → AD-05 → RA-01 → RV-01 → RV-02 → RV-03 → CO-01 → CO-02 → CO-04 → CO-05 → CM-01 → CM-03 → CM-04 → AR-01 → AR-02 → AR-05 → AR-06 → SC-02 → SC-04 → SC-05.
3. Chạy các ca từ chối trên nhóm riêng. Không dùng nhóm đã khóa/công bố để thử quay ngược trạng thái.
4. Sau mỗi ca, điền **thực tế**, **trạng thái**, **bằng chứng** trong bảng. Refresh để kiểm tra dữ liệu thực sự đã lưu.
5. Nếu thất bại, ghi lỗi ở mục 7 với cùng mã test case. Không sửa trực tiếp database để làm ca kiểm thử đạt.

Quy ước trạng thái:

| Giá trị | Ý nghĩa |
| --- | --- |
| Chưa chạy | Chưa thao tác ca này trên môi trường mục tiêu. |
| Pass | Đã chạy đầy đủ bước và kết quả đúng mong đợi. |
| Fail | Đã chạy và có khác biệt với mong đợi. |
| Blocked | Không thể chạy vì thiếu dữ liệu, quyền, dịch vụ hoặc bước trước chưa hoàn tất. Ghi nguyên nhân. |
| N/A | Không áp dụng cho cấu hình đang test; phải nêu lý do. |

Tests tự động đạt không tự động làm một test case UI thành Pass. Mọi hàng checklist bên dưới khởi tạo là **Chưa chạy**.

## 2. Những gì đã xác minh trong phiên lập báo cáo

| Hạng mục | Kết quả thực tế | Bằng chứng / giới hạn |
| --- | --- | --- |
| .NET SDK | `10.0.404` | Kiểm tra `dotnet --version`. |
| Node.js | `v24.16.0` | Kiểm tra `node --version`. |
| Frontend local | GET `/` trả HTTP 200 | Chỉ xác minh HTTP, chưa xác minh đăng nhập hay luồng UI. |
| Backend health probe | GET `/health` trả HTTP 404 | Không kết luận backend ngừng chạy; đường dẫn này không chứng minh health của ứng dụng. |
| Frontend tests chọn lọc | **40 Pass, 0 Fail, 0 Skip** | 9 file tests; log `artifacts/final-report-test/frontend-tests.log`. Kiểm tra logic/render có kiểm soát dependency, không phải browser E2E. |
| Backend tests chọn lọc — lượt cuối | **221 Pass, 0 Fail, 0 Skip** | `artifacts/final-report-test/defense-selected-retry.trx`, log `backend-retry.log` cùng thư mục. Tắt riêng EventLog logging trong tiến trình test; không sửa code/quyền hệ thống. Các HTTP tests ở bộ này dùng TestServer và dependencies có kiểm soát, không phải PostgreSQL/MinIO E2E. |
| Backend tests — lượt đầu để chẩn đoán | 53 Pass, 168 Fail trong tổng 221 | `defense-selected.trx`: cả 168 ca lỗi cùng thông báo `Cannot open log for source '.NET Runtime' ... Access is denied` khi khởi tạo host. Sau cấu hình logging theo tiến trình, chạy lại toàn bộ 221 ca và đều đạt. Không cộng hai lượt thành 442 ca. |
| Integration tests dùng Testcontainers | Chưa chạy — môi trường bị chặn | `docker info`: `Error loading config file ... .docker/config.json: Access is denied`; `failed to connect ... npipe:////./pipe/docker_engine ... The system cannot find the file specified`. Chưa xác định Docker chưa chạy hay chưa truy cập được engine. |
| Browser verification | Chưa chạy — môi trường bị chặn | Wrapper Playwright trả `PSSecurityException`: `running scripts is disabled on this system`. Không thay đổi execution policy. |

Kết quả tự động cuối: **261 ca chọn lọc đạt (40 frontend + 221 backend), 0 Fail/Skip**. Con số này không phải kết quả của 59 ca thao tác tay và không phải toàn solution. Lượt backend cuối có cảnh báo `VSTEST_ARTIFACTS_DIRECTORY environment variable was not found`; TRX vẫn được tạo tại thư mục chỉ định.

Không seed/reset/migrate database ứng dụng trong phiên này. Không thay đổi application code.

## 3. Chuẩn bị trước khi test

### 3.1. Tài khoản

| Ký hiệu | Vai trò và quan hệ cần có | Tài khoản thực tế |
| --- | --- | --- |
| SV-A1 / SV-A2 | Hai Student Active, Confirmed membership cùng nhóm A | ................................ |
| SV-X | Student ngoài nhóm A, dùng kiểm tra truy cập chéo | ................................ |
| HD-A | Lecturer là GVHD hiện tại của đề tài A | ................................ |
| PB-A | Lecturer Active khác GVHD; được phân công phản biện A | ................................ |
| KHOA | Faculty có permission phân công, đọc/chấm theo phạm vi, lưu chiểu, tính và công bố điểm cần thiết | ................................ |
| PDT | AcademicAffairs, dùng phê duyệt Hội đồng và đọc/xuất kết quả | ................................ |
| CT / TK / UV | Chủ tịch, Thư ký, Ủy viên; Lecturer và role CouncilMember khi vào màn chấm Hội đồng | ................................ |
| ADMIN | Admin dự phòng cho API và nghiệp vụ được cho phép | ................................ |

Role trên frontend không thay thế quyền nghiệp vụ backend. Dùng tài khoản đúng quan hệ nhóm/phân công để kiểm tra quyền thực tế. Sau khi được cấp role mới, đăng nhập lại nếu phiên hiện tại chưa nhận role.

README có tài khoản demo, nhưng chỉ sử dụng khi xác nhận chúng đã được seed trên database local. Không coi dữ liệu demo có sẵn là một lượt chạy thành công từ đầu.

### 3.2. Dữ liệu và dịch vụ

- Một đợt Open, có thời gian nộp báo cáo và lưu chiểu còn hiệu lực; barem chấm điểm và cấu hình điểm Active hợp lệ.
- Nhóm A đã qua giữa kỳ, có báo cáo cuối kỳ v1 vừa nộp ở `Submitted`. Nếu chưa nộp, thực hiện nộp trước và ghi file/thời điểm.
- Nhóm B dùng nhánh GVHD chưa đồng ý; nhóm C dùng nhánh GVPB Rejected hoặc dưới ngưỡng; nhóm D dùng hủy/phân công lại; nhóm E dùng lưu chiểu yêu cầu sửa và kiểm tra publication lỗi.
- PDF hợp lệ v1/v2, DOCX hợp lệ, file sai định dạng, file quá giới hạn và nội dung metadata tiếng Việt/Anh. Lưu file vào thư mục test riêng.
- PostgreSQL phát triển, storage MinIO/S3 và backend/frontend hoạt động. AI/worker hoạt động nếu chạy ca AI. Integration tests tự động cần Docker/Testcontainers riêng.
- Không dùng GVHD của nhóm A làm CT/TK/UV của Hội đồng chấm nhóm A.

### 3.3. Sổ định danh

| Dữ liệu | Giá trị |
| --- | --- |
| groupId A / B / C / D / E | ................................ |
| topicId / batchId | ................................ |
| finalReportId / currentVersionNo | ................................ |
| advisorEvaluationId / evaluationVersion | ................................ |
| reviewerAssignmentId / assignmentVersion | ................................ |
| reviewerEvaluationId / evaluationVersion | ................................ |
| councilId / councilAssignmentId | ................................ |
| finalSubmissionId / submissionVersion / packageVersion | ................................ |
| scoreSummaryId / summaryVersion | ................................ |

Luôn lấy version mới từ response/GET. `currentVersionNo` của báo cáo, `assignmentVersion`, `evaluationVersion`, `submissionVersion` và `summaryVersion` không phải cùng một loại version.

## 4. Checklist kiểm thử từng tính năng

Trong cột cuối, điền theo mẫu: `Thực tế: ...; Trạng thái: Pass/Fail/Blocked; Bằng chứng: đường dẫn ảnh hoặc mã request đã loại thông tin nhạy cảm`.

### 4.1. Báo cáo cuối kỳ và khóa phiên bản

Màn hình SV: `/thesis/final-report`.

| ID | Tài khoản / tiền điều kiện | Bước thực hiện | Kết quả mong đợi | Thực tế / trạng thái / bằng chứng |
| --- | --- | --- | --- | --- |
| FR-01 | SV-A1; đã nộp v1 | Mở màn báo cáo; refresh; đối chiếu tên file, version, thời gian và trạng thái | Có v1 `Submitted`; nhóm `FinalReportSubmitted`; dữ liệu không mất sau refresh | Chưa chạy |
| FR-02 | SV-A1/A2; cùng nhóm A | Xem lịch sử, tải PDF; kiểm tra link sản phẩm/ảnh nếu đã nộp | Đúng hồ sơ nhóm, file tải được và khớp nội dung đã nộp; thành viên cùng nhóm xem đúng dữ liệu | Chưa chạy |
| FR-03 | Nhóm thử riêng; còn hạn, `Submitted` | Nộp PDF v2, xem lại lịch sử; tải cả v1 và v2 | Version tăng; giữ hai phiên bản và đúng nội dung từng file | Chưa chạy |
| FR-04 | SV-X | Gọi API history/download bằng ID của nhóm A | Từ chối truy cập; không trả file/link riêng tư | Chưa chạy |
| FR-05 | Nhóm thử; hết hạn hoặc đợt không Open | Thử nộp mới/nộp lại | Bị chặn; dữ liệu/version trước không thay đổi | Chưa chạy |
| FR-06 | A đã `UnderEvaluation` sau AD-01 | Thử nộp lại InitialFinal bằng UI/API | Bị chặn; không thay phiên bản đã khóa | Chưa chạy |

### 4.2. GVHD chấm và xét chuyển phản biện

Màn hình: `/thesis/lecturer-grading`, chọn phần GVHD. Lần lưu nháp/lưu điểm hoặc quyết định chuyển phản biện đầu tiên tự gọi `begin-evaluation` cho đúng phiên bản đang xem trước khi lưu phiếu.

| ID | Tài khoản / tiền điều kiện | Bước thực hiện | Kết quả mong đợi | Thực tế / trạng thái / bằng chứng |
| --- | --- | --- | --- | --- |
| AD-01 | HD-A; InitialFinal `Submitted` | Nhập một phần điểm và lưu nháp; kiểm tra Network và history | UI gọi begin-evaluation đúng version trước khi lưu; báo cáo thành UnderEvaluation; SV không nộp đè được nữa. Không cần gọi API thủ công | Chưa chạy |
| AD-02 | HD-A; `UnderEvaluation`, barem hợp lệ | Nhập một phần điểm từng SV, lưu nháp rồi refresh | Nháp và điểm nhập được lưu; không tự chuyển phản biện | Chưa chạy |
| AD-03 | HD-A | Nhập đủ tiêu chí từng SV, nhận xét; chốt điểm; đọc lại | Điểm tiêu chí/tổng điểm từng SV đúng; trạng thái chấm cập nhật; chưa tự có phân công GVPB | Chưa chạy |
| AD-04 | Nhóm B; phiếu còn cho phép quyết định | Chọn chưa đồng ý; thử thiếu lý do rồi nhập lý do và gửi | Thiếu lý do bị từ chối; có lý do được lưu; nhóm giữ `FinalReportSubmitted`; không tự mở quyền SV nộp lại | Chưa chạy |
| AD-05 | HD-A; phiếu hoàn chỉnh | Đồng ý chuyển phản biện, refresh | Nhóm `ReadyForReviewerAssignment`; phiếu GVHD bị khóa | Chưa chạy |
| AD-06 | HD-A; sau AD-05 | Thử sửa phiếu điểm bằng UI/API | Bị chặn, không ghi đè phiếu đã khóa | Chưa chạy |
| AD-07 | Lecturer khác không phải HD-A | Thử lưu/chốt phiếu của A | Từ chối quyền; phiếu không thay đổi | Chưa chạy |

### 4.3. Phân công phản biện

Màn hình: `/councils/reviewer-assignments`.

| ID | Tài khoản / tiền điều kiện | Bước thực hiện | Kết quả mong đợi | Thực tế / trạng thái / bằng chứng |
| --- | --- | --- | --- | --- |
| RA-01 | KHOA; A được GVHD đồng ý | Lọc đúng đợt, chọn A và PB-A, phân công rồi refresh | Một assignment active; PB-A nhận đúng task/báo cáo/phiên bản đã khóa | Chưa chạy |
| RA-02 | KHOA | Thử chọn chính GVHD; thử phân công active thứ hai cho A | Bị chặn; không sinh hai phân công active | Chưa chạy |
| RA-03 | Nhóm D; chưa có phiếu PB | Hủy phân công với lý do; phân công lại cùng/khác GVPB | Assignment cũ giữ lịch sử, không active; assignment mới hợp lệ | Chưa chạy |
| RA-04 | Nhóm D; đã có phiếu Draft | Thử hủy phân công | Bị chặn dù phiếu mới là nháp; không dùng nhóm A cho ca này | Chưa chạy |
| RA-05 | GVPB cũ của D sau hủy | Xin URL tải mới bằng assignment/file cũ | Không được cấp quyền tải mới qua phân công đã hủy; URL ký trước đó có thể còn hiệu lực đến hết TTL | Chưa chạy |

### 4.4. GVPB chấm, góp ý và bản sửa sau phản biện

GVPB: `/thesis/lecturer-grading`, chọn phần phản biện. SV: `/thesis/final-report`.

| ID | Tài khoản / tiền điều kiện | Bước thực hiện | Kết quả mong đợi | Thực tế / trạng thái / bằng chứng |
| --- | --- | --- | --- | --- |
| RV-01 | PB-A | Mở task, xem/tải báo cáo | Đúng nhóm và snapshot báo cáo được phân công; không lấy nhầm bản khác | Chưa chạy |
| RV-02 | PB-A; chưa chốt | Lưu Draft một phần điểm và nhận xét; SV kiểm tra góp ý | Nháp lưu được; SV chưa thấy comment nháp | Chưa chạy |
| RV-03 | PB-A | Nhập đủ tiêu chí cho mọi SV, comment và Approved; chốt rồi thử sửa | Chốt thành công, phiếu `Submitted` bị khóa; SV chỉ nhận comment được phát hành qua API feedback | Chưa chạy |
| RV-04 | Nhóm C | Chốt Rejected hoặc điểm một SV dưới ngưỡng cấu hình; kiểm tra eligibility | Chặn xếp Hội đồng; không tự mở lại phiếu/nộp bài/phân công PB thứ hai | Chưa chạy |
| RV-05 | SV-A1; gate đạt, còn hạn | Nộp bản sửa sau phản biện; xem lịch sử | Tạo hồ sơ `RevisedAfterReviewer` riêng, không ghi đè InitialFinal và snapshot phiếu cũ | Chưa chạy |
| RV-06 | SV-X / Lecturer không được phân công | Thử đọc feedback/phiếu hoặc chấm A bằng ID trực tiếp | Từ chối truy cập ngoài phạm vi; không lộ phiếu điểm riêng tư | Chưa chạy |

### 4.5. Lập Hội đồng, xếp lịch và phê duyệt

Khoa: `/councils/members`. PĐT: `/councils/approval`.

| ID | Tài khoản / tiền điều kiện | Bước thực hiện | Kết quả mong đợi | Thực tế / trạng thái / bằng chứng |
| --- | --- | --- | --- | --- |
| CO-01 | KHOA; A đủ gate phản biện | Tạo Hội đồng Draft; đặt ngày/ca/phòng; thêm CT, TK, UV | Lưu được cấu trúc hợp lệ; đúng thành viên và vai trò | Chưa chạy |
| CO-02 | KHOA | Gán A và thứ tự bảo vệ | Đúng đợt; A chỉ thuộc một assignment Hội đồng; thứ tự không trùng | Chưa chạy |
| CO-03 | Nhóm/Hội đồng thử riêng | Thử gán nhóm C, GVHD vào Hội đồng nhóm mình, GVPB làm CT/TK, trùng phòng/ca hoặc trùng lịch thành viên | Từ chối từng trường hợp tương ứng; không lưu cấu hình bất hợp lệ | Chưa chạy |
| CO-04 | KHOA | Gửi hồ sơ Hội đồng sang PĐT | Ghi nhận đã gửi thẩm định, hồ sơ đủ thành viên và nhóm | Chưa chạy |
| CO-05 | PDT | Xem hồ sơ, nhập số quyết định và phê duyệt | Hội đồng Active; thành viên được cấp role CouncilMember; vào được workspace phù hợp sau cập nhật phiên | Chưa chạy |
| CO-06 | Hội đồng thử | Thử phê duyệt khi chưa gửi PĐT hoặc thiếu dữ liệu bắt buộc | Bị chặn, không tự Active | Chưa chạy |

### 4.6. Chấm Hội đồng, biên bản và mở khóa

Màn hình: `/councils/grading`, `/councils/minutes`.

| ID | Tài khoản / tiền điều kiện | Bước thực hiện | Kết quả mong đợi | Thực tế / trạng thái / bằng chứng |
| --- | --- | --- | --- | --- |
| CM-01 | CT/TK/UV; Hội đồng Active | Mỗi người nhập điểm từng SV theo barem; đọc lại | Lưu đúng người chấm/nhóm/SV; chưa tự công bố điểm | Chưa chạy |
| CM-02 | PB-A kiêm Member | Thử nhập thêm phiếu điểm Hội đồng thứ hai cho A | Bị chặn cộng trùng; dùng điểm phản biện theo cấu hình | Chưa chạy |
| CM-03 | TK hoặc actor được phép | Mở workspace, kiểm tra đầu điểm/chênh lệch; nhập yêu cầu sửa và lưu biên bản | Nội dung được lưu; chênh lệch/điều kiện khóa được phản ánh đúng cấu hình; loại đầu điểm phải có lý do | Chưa chạy |
| CM-04 | TK hoặc actor được phép; đủ điều kiện | Khóa biên bản; thử mật khẩu sai trước, sau đó dùng mật khẩu đúng | Sai bị từ chối; đúng khóa `Locked`; không cho sửa nguồn điểm khi đang khóa | Chưa chạy |
| CM-05 | KHOA có permission mở khóa hoặc ADMIN; chưa công bố | Yêu cầu mở khóa với lý do, sửa rồi khóa lại | Mở khóa đúng quyền, lưu lý do/audit; các bước sau kiểm tra lại proof/version | Chưa chạy |
| CM-06 | Sau SC-04 | Thử mở khóa/sửa biên bản sau công bố | Bị chặn; không thay nguồn kết quả đã công bố | Chưa chạy |

### 4.7. Lưu chiểu và xác nhận hai bước

SV: `/thesis/archival`; GVHD: `/advisor/archival`; Khoa: `/thesis/review/archival`.

| ID | Tài khoản / tiền điều kiện | Bước thực hiện | Kết quả mong đợi | Thực tế / trạng thái / bằng chứng |
| --- | --- | --- | --- | --- |
| AR-01 | SV-A1; biên bản Locked | Mở trang và kiểm tra eligibility/window | Được nộp khi đủ điều kiện; thiếu proof/không đúng hạn thì có lý do chặn | Chưa chạy |
| AR-02 | SV-A1 | Nộp PDF + DOCX hợp lệ, tóm tắt và từ khóa VI/EN | Package v1 `Submitted`; tải lại được đúng hai file và metadata | Chưa chạy |
| AR-03 | Nhóm thử | Thử thiếu DOCX, file giả định dạng, file quá 20 MiB hoặc thiếu metadata bắt buộc | Từ chối; không tạo package hoàn chỉnh giả hay làm tăng version thành công | Chưa chạy |
| AR-04 | Nhóm E; trước khi khóa lưu chiểu | GVHD yêu cầu sửa → SV nộp v2; sau xác nhận GVHD, thử Khoa yêu cầu sửa → SV nộp bản mới | `NeedRevision → Submitted`; giữ lịch sử cũ; phải xác nhận lại trên package mới | Chưa chạy |
| AR-05 | HD-A; package Submitted | Xác nhận package hiện tại | Chuyển `AdvisorConfirmed`, lưu actor/thời điểm; SV không tự nộp đè khi đang trạng thái này | Chưa chạy |
| AR-06 | KHOA; AdvisorConfirmed | Xác nhận lưu chiểu | Chuyển `ArchiveConfirmed`, khóa bất biến | Chưa chạy |
| AR-07 | KHOA / SV; nhóm thử | Thử Khoa xác nhận thẳng Submitted; sau ArchiveConfirmed thử sửa/nộp lại | Bị chặn cả bỏ qua GVHD và sửa hồ sơ đã khóa | Chưa chạy |
| AR-08 | Hai phiên duyệt/nộp cùng hồ sơ | Phiên 1 đọc v1; phiên 2 nộp v2; phiên 1 xác nhận v1 | Xung đột version, không xác nhận nhầm package mới bằng dữ liệu cũ | Chưa chạy |

### 4.8. Tính điểm, công bố và kết quả sinh viên

Màn hình: `/councils/results`. Cấu hình: `/admin/grade-config`.

| ID | Tài khoản / tiền điều kiện | Bước thực hiện | Kết quả mong đợi | Thực tế / trạng thái / bằng chứng |
| --- | --- | --- | --- | --- |
| SC-01 | KHOA; nhóm thiếu lưu chiểu/biên bản/đầu điểm | Kiểm tra readiness và thử tính/công bố | Bị chặn đúng nguyên nhân; không tự bù điểm 0 hay bỏ qua proof | Chưa chạy |
| SC-02 | KHOA; A đủ điều kiện | Tính điểm; đối chiếu từng thành phần và tổng bằng cấu hình Active | Có bản tính dự thảo đúng trọng số, làm tròn cuối 2 số; kết quả từng SV phản ánh điểm của SV đó | Chưa chạy |
| SC-03 | Nhóm thử; trước công bố | Tính dự thảo, đổi một nguồn/config được phép rồi kiểm tra stale và thử công bố bản cũ | Phát hiện stale/chặn công bố dữ liệu cũ; tính lại đúng dữ liệu hiện tại | Chưa chạy |
| SC-04 | KHOA; summary hiện tại hợp lệ | Công bố A; refresh | `PublishedToStudent`, lưu thông tin công bố; khóa tính lại/sửa nguồn liên quan | Chưa chạy |
| SC-05 | SV-A1 / SV-A2 | Trước công bố thử xem điểm; sau công bố xem lại bằng từng tài khoản | Trước công bố chưa được xem chính thức; sau công bố có đúng điểm/thành phần của SV, không lộ dữ liệu SV ngoài phạm vi | Chưa chạy |
| SC-06 | KHOA; hai nhóm thử | Chọn công bố hai nhóm, một nhóm không hợp lệ hoặc stale | Toàn bộ selection thất bại; nhóm hợp lệ không bị công bố một phần | Chưa chạy |
| SC-07 | PDT | Đọc kết quả, thử calculate/publish | Đọc theo quyền; không được tính/công bố chỉ với role AcademicAffairs | Chưa chạy |
| SC-08 | KHOA/PDT/ADMIN | Lọc đợt/trạng thái; xuất XLSX và PDF | File mở được, đúng dữ liệu/bộ lọc và phạm vi quyền | Chưa chạy |

### 4.9. Tương đồng AI và kiểm tra API bổ sung

| ID | Tài khoản / tiền điều kiện | Bước thực hiện | Kết quả mong đợi | Thực tế / trạng thái / bằng chứng |
| --- | --- | --- | --- | --- |
| AI-01 | Có AI/worker, báo cáo mới nộp | Theo dõi lượt scan theo report/version | Có tiến trình/kết quả hoặc trạng thái lỗi/chờ rõ ràng; không gán kết quả bản cũ cho bản mới | Chưa chạy |
| AI-02 | SV-A1 rồi HD-A | Xem cùng kết quả scan | SV chỉ thấy receipt; GVHD có quyền xem phân tích chi tiết; không lộ evidence chi tiết qua response SV | Chưa chạy |
| AI-03 | HD-A/ADMIN | Retry lượt terminal; thử retry lượt đang xử lý | Terminal được xử lý theo guard; lượt đang xử lý bị chặn retry trùng | Chưa chạy |
| AI-04 | CouncilMember được phân công | Vào `/councils/similarity-reports`; so với member ngoài Hội đồng | Chỉ đọc đúng nhóm được phân công và trạng thái báo cáo cho phép | Chưa chạy |
| API-01 | Write API có yêu cầu Idempotency-Key | Gửi lại cùng UUID và payload, sau đó đổi payload nhưng giữ key | Replay không tạo ghi/audit đôi; cùng key khác ý định bị từ chối | Chưa chạy |
| API-02 | Có hai phiên cùng sửa | Gửi request với evaluation/submission/summary version cũ | Trả xung đột, không ghi đè thay đổi mới; UI yêu cầu tải lại phù hợp | Chưa chạy |
| LIB-01 | Có cấu hình xuất bản thư viện | Tra cứu `/library`, mở chi tiết và tài liệu bằng tài khoản đúng/sai quyền | Chỉ hiện hồ sơ được cấu hình công bố; không coi lưu chiểu xong là tự công khai mọi file | Chưa chạy |

## 5. Bắt đầu chấm khi sử dụng API trực tiếp

UI đã tự thực hiện bước này trong lần lưu đầu tiên. Các bước dưới đây chỉ dùng khi kiểm thử API trực tiếp hoặc chẩn đoán; không cần thực hiện để dùng màn chấm GVHD.

1. Đăng nhập GVHD đúng đề tài hoặc Admin trên môi trường test.
2. Mở `http://localhost:5287/scalar`, dùng Bearer token của tài khoản đó. Không ghi token vào báo cáo hoặc ảnh.
3. Đọc history để lấy `finalReportId` và `currentVersionNo` hiện tại:

   ```http
   GET /api/thesis/groups/{groupId}/final-report
   ```

4. Gọi:

   ```http
   POST /api/thesis/final-reports/{finalReportId}/begin-evaluation
   Content-Type: application/json

   { "expectedVersionNo": 1 }
   ```

   Thay `1` bằng version thực tế vừa đọc. Chỉ thực hiện trên nhóm test đã chọn.

5. Đọc lại history, xác nhận `UnderEvaluation`, rồi refresh màn chấm GVHD và tiếp tục AD-02.
6. Khi test UI, không gọi bước này trước: cần xác minh chính UI gọi begin-evaluation rồi mới lưu phiếu. Nếu begin trả lỗi, UI phải dừng lưu, giữ nội dung đang nhập và không tự đổi sang version mới.

## 6. Lệnh chạy lại tests tự động

Chạy từ root UGTM. Không dùng kết quả lịch sử thay cho kết quả lần chạy hiện tại.

### Frontend — bộ chọn lọc đã chạy

```powershell
node --test frontend/test/academic-results.test.mjs frontend/test/archival.test.mjs frontend/test/council-approval.test.mjs frontend/test/council-members.test.mjs frontend/test/council-member-workspace.test.mjs frontend/test/council-planning.test.mjs frontend/test/student-results.test.mjs frontend/test/similarity.test.mjs frontend/test/similarity-report.test.mjs
```

### Backend — bộ chọn lọc

Nếu gặp lỗi Windows Event Log trong sandbox, đặt biến cấu hình chỉ cho tiến trình PowerShell đang chạy tests. Lưu và khôi phục giá trị cũ như lệnh dưới; không thay đổi Windows execution policy hoặc quyền Event Log.

```powershell
$previousTestEventLogLevel = $env:Logging__EventLog__LogLevel__Default
try {
    $env:Logging__EventLog__LogLevel__Default = 'None'
    dotnet test backend/tests/DefenseCouncil.Tests/UGTM.Modules.DefenseCouncil.Tests.csproj --no-restore --filter 'FullyQualifiedName~AdvisorAndReviewerGradingCriteriaTests|FullyQualifiedName~CouncilDefenseEligibilityTests|FullyQualifiedName~CouncilMinuteWorkspaceTests|FullyQualifiedName~CouncilMinuteLockUnlockTests|FullyQualifiedName~ScoreSummaryResolverTests|FullyQualifiedName~AcademicResultsTests' --logger 'trx;LogFileName=defense-selected-retry.trx' --results-directory artifacts/final-report-test
} finally {
    $env:Logging__EventLog__LogLevel__Default = $previousTestEventLogLevel
}
```

`--no-restore` yêu cầu dependencies đã restore. Bộ này không thay thế integration tests liên module hoặc kiểm thử UI.

### Integration — chỉ chạy khi Docker/Testcontainers hoạt động

```powershell
dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --filter 'FullyQualifiedName~FinalReportSubmissionHttpIntegrationTests|FullyQualifiedName~FinalReportResubmissionHttpIntegrationTests|FullyQualifiedName~FinalReportEvaluationHttpIntegrationTests|FullyQualifiedName~FinalReportReadHttpIntegrationTests|FullyQualifiedName~FinalReportWorkflowIntegrationTests' --logger 'trx;LogFileName=workflow-integration.trx' --results-directory artifacts/final-report-test
```

Filter theo tên lớp chỉ chọn các tests hiện có trong lớp đó. Một số tests tích hợp seed trạng thái upstream; không coi toàn bộ kết quả là E2E từ đầu qua UI. Xem source fixture khi đánh giá độ bao phủ.

## 7. Nhật ký lỗi và vấn đề cần xác minh

| Mã | Nguồn / test case | Vấn đề | Trạng thái / bước tiếp theo |
| --- | --- | --- | --- |
| BUG-02 | Người dùng tái hiện AD-02; InitialFinal Submitted | SV thấy Chờ GVHD chấm, nhưng UI gửi lưu phiếu trước khi bắt đầu chấm, bị InvalidEvaluationState | Đã nối begin-evaluation vào lần lưu nháp/lưu điểm và quyết định đầu tiên, giữ đúng phiên bản đã xem. Sau begin thành công, giữ trạng thái UnderEvaluation trên UI để retry lưu không begin lại. 8 tests mới kiểm tra Submitted/UnderEvaluation, metadata thiếu, phiên bản xung đột, response sai binding và API request; không đổi guard backend. Browser với API giả lập đã xác minh hai luồng: begin → SaveDraft → Submit → approve và begin → Submit → approve; giao diện mobile không tràn ngang. 28 frontend tests liên quan, lint và build đạt (còn warnings sẵn có). Chưa lưu/duyệt trên nhóm thật của người dùng. |
| OBS-02 | Rà soát source; RV-05/CO-02 | Có hồ sơ RevisedAfterReviewer nhưng gate xếp Hội đồng chưa thấy bắt buộc phải nộp bản sửa | Ghi nhận hành vi hiện tại; cần thống nhất yêu cầu trước khi gọi là lỗi. |
| BUG-01 | Người dùng tái hiện AD-02/AD-05; payload Network | Lưu nháp/duyệt GVHD trả 400 `AdvisorEvaluation.InvalidRequest`: frontend gửi `criteriaId` là mã hiển thị `1.1`, `1.2` thay vì GUID | Đã khôi phục barem cứng UIT 10 tiêu chí/thang 10. API tự bổ sung tiêu chí chuẩn khi đợt chưa có dữ liệu, giữ nguyên ID/điểm đã lưu; không cần người dùng cấu hình barem. Frontend giữ nguyên tiêu chí/tên/điểm tối đa cũ và ánh xạ mã → GUID thật. 29 frontend tests và 9 integration tests PostgreSQL/MinIO đạt, gồm HTTP lưu nháp → lưu điểm 8.5 → duyệt chuyển phản biện, nhóm ReadyForReviewerAssignment. Lint/build đạt, còn warning ngoài phần sửa. Container backend local đã build/chạy lại. Chưa thao tác lưu/duyệt trên nhóm thật của người dùng. |
| ENV-01 | Integration tests | Docker config bị từ chối truy cập; không kết nối được pipe engine | Chưa chạy integration; xác minh Docker Desktop/engine và quyền truy cập. |
| ENV-02 | Browser verification | Playwright PowerShell wrapper bị execution policy chặn | Chưa chạy browser; chưa thay chính sách hệ thống. |
| ENV-03 | Backend tests chọn lọc | Lượt đầu lỗi quyền Windows Event Log, 168 ca không qua khởi tạo host | Đã kiểm tra lại bằng cấu hình logging riêng cho tiến trình: toàn bộ 221 ca đạt. Giữ TRX lượt đầu để truy vết, không tính là 168 lỗi nghiệp vụ. |

Mẫu bổ sung lỗi thực tế:

```text
Mã lỗi / Test case:
Mức ảnh hưởng:
Môi trường / commit:
Tài khoản và quan hệ với nhóm (không ghi mật khẩu/token):
groupId / reportId / version:
Tiền điều kiện:
Các bước tái hiện:
Kết quả mong đợi:
Kết quả thực tế / HTTP status / error.code:
Bằng chứng (ảnh, log đã loại thông tin nhạy cảm):
Kết quả kiểm tra dữ liệu sau refresh:
Trạng thái xử lý / ngày retest:
```

## 8. Tổng kết sau khi hoàn tất thao tác tay

- Tổng test case UI/API trong checklist: 59.
- Pass: .... / Fail: .... / Blocked: .... / N/A: .... / Chưa chạy: ....
- Luồng thành công xuyên suốt đã chạy thực tế: Có / Chưa.
- Các bước phải dùng API thay UI: ................................
- Lỗi ngăn luồng chính: ................................
- Giới hạn kiểm thử / dịch vụ chưa có: ................................
- Người kiểm thử / ngày chốt: ................................

## 9. Nguồn đối chiếu

- [README — môi trường local và tài khoản demo](../README.md).
- [Frontend router](../frontend/src/app/router.tsx).
- [Endpoint báo cáo và bắt đầu chấm](../backend/Modules/ThesisManagement/Infrastructure/Endpoints/FinalReportSubmissionEndpoints.cs).
- [Frontend API chấm GVHD/GVPB](../frontend/src/features/thesis/services/lecturerGradingApi.ts).
- [Contract GVHD](ApiContract/AdvisorEvaluationApprovalApiContract.md).
- [Contract phân công GVPB](ApiContract/ReviewerAssignmentApiContract.md).
- [Contract chấm phản biện](ApiContract/ReviewerEvaluationApiContract.md).
- [Contract lưu chiểu](ApiContract/FinalSubmissionApiContract.md).
- [Contract tính/công bố điểm](ApiContract/ScoreCalculationPublicationApiContract.md).
- [Tests chấm theo tiêu chí và từng SV](../backend/tests/DefenseCouncil.Tests/AdvisorAndReviewerGradingCriteriaTests.cs).
- [Tests biên bản](../backend/tests/ThesisManagement.Tests/Integration/CouncilMinuteWorkflowIntegrationTests.cs).
- [Tests lưu chiểu đến công bố](../backend/tests/ThesisManagement.Tests/Integration/ArchivalScoreEndToEndIntegrationTests.cs).

Một số contract ghi phạm vi tại checkpoint cũ, chưa mô tả đầy đủ UI/tiêu chí/từng SV hiện tại. Khi có khác biệt, đối chiếu source và response thực tế; không lấy câu “ngoài phạm vi” của checkpoint cũ làm kết luận tính năng hiện chưa có.

