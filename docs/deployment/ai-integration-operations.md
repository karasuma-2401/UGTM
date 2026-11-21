# Vận hành tích hợp kiểm tra tương đồng

Source AI hiện ở [`services/plagiarism-ai`](../services/plagiarism-ai/MONOREPO.md) trong cùng repository. Triển khai production theo [runbook Azure/Vercel/Modal](production-deployment.md); AI API, worker, reconciler vẫn là tiến trình riêng, dùng DB AI và bucket riêng.

UGTM và dịch vụ `UGTM-Plagiarism-AI` phải được cập nhật cùng nhau: chức năng quét lại dùng endpoint `POST /api/v1/jobs/{jobId}/retry` của dịch vụ AI. Cấu hình UGTM bằng `PlagiarismAi__BaseUrl`, `PlagiarismAi__ApiKey` và `PlagiarismAi__DefaultCorpus`. API key phải khớp `API_KEY` của dịch vụ AI; corpus phải chứa dữ liệu nguồn đã được lập chỉ mục. Không đưa key thật vào mã nguồn. Nếu UGTM chạy trong container, `localhost` là container UGTM, không phải máy host hay container AI.

Áp dụng các migration của UGTM trước khi chạy phiên bản mới. Trên production, migration là bước riêng sau backup và review; deploy chỉ kiểm tra schema, không tự áp dụng. Nếu DB đã có migration mà image chưa chứa, dùng release phù hợp và làm theo [hướng dẫn recovery](production-operations-runbook.md#migration-và-rollback), không chỉnh migration history để vượt gate. Migration `BackfillFinalReportPlagiarismScans` tạo lượt quét đang chờ cho từng phiên bản báo cáo cuối kỳ hiện có chưa có scan; worker sẽ tự gửi chúng tới AI khi được cấu hình. Cần dự tính dung lượng hàng đợi và tài nguyên xử lý cho dữ liệu cũ.

AI tự lên lịch thử lại các job `waiting_for_compute`; UGTM giữ nguyên job ID và theo dõi trạng thái. Khi job đã hết số lần thử và chuyển sang `ComputeUnavailable`, quản trị viên hoặc giảng viên hướng dẫn được phân công có thể dùng endpoint retry sau khi GPU sẵn sàng. Quản trị viên hoặc người có quyền khoa có thể khởi động lại hàng loạt bằng `resume-pending`. Health check của API không xác nhận GPU sẵn sàng.

Endpoint `index-corpus` chỉ xác nhận yêu cầu đã được tiếp nhận. Dùng `jobId` trong kết quả gọi `GET /api/thesis/similarity-index-jobs/{jobId}` để theo dõi kết quả. Chỉ người quản trị hoặc người có quyền khoa mới truy cập được trạng thái này.
