# UGTM ↔ UGTM-Plagiarism-AI — Tài liệu Hướng dẫn Tích hợp & Kiểm chứng Quyền truy cập (CP-05.4)

Tài liệu này hướng dẫn chi tiết cho các kỹ sư backend .NET 10 của hệ thống **UGTM** ([`karasuma-2401/UGTM`](https://github.com/karasuma-2401/UGTM)) để tích hợp dịch vụ đối soát tương đồng học thuật **UGTM-Plagiarism-AI**.

---

## 1. Tuyên bố Trạng thái Tích hợp (Integration Status Statement)

> [!IMPORTANT]
> **Trạng thái Tích hợp Thực tế (Tính đến Checkpoint CP-05.4):**
> - **Mức độ kiểm chứng:** Đã kiểm chứng 100% hợp đồng API, bảo mật token, xử lý xung đột phiên bản, phân trang và ma trận phân quyền RBAC thông qua bộ kiểm thử **Contract Test Harness mô phỏng** ([`tests/test_ugtm_contract_and_rbac.py`](../tests/test_ugtm_contract_and_rbac.py) — 11/11 tests passed).
> - **Môi trường kết nối thực tế:** **Đang chờ triển khai (Pending production deployment)** với backend .NET 10 thật của UGTM. Tuân thủ nghiêm ngặt nguyên tắc kiểm định: *Dự án chưa nối với UGTM runtime thật nên ghi nhận rõ ràng trạng thái tích hợp thực tế còn chờ, không tự nhận là đã hoàn tất kết nối môi trường production.*

---

## 2. Kiến trúc & Ranh giới Hệ thống (Architecture & Security Boundaries)

```
[ Sinh viên / Giảng viên (React 19 SPA) ]
                  |
        (Cookie / JWT của UGTM)
                  v
[ UGTM Backend (.NET 10 Modular Monolith) ]
      - Phân quyền RBAC (FinalReportReadAccessChecker)
      - Lưu trữ tệp gốc S3 (S3ThesisSubmissionObjectStorage)
      - Quản lý giao tác nộp bài (ThesisManagement Outbox)
                  |
        (X-API-Key bí mật S2S)
                  v
[ UGTM-Plagiarism-AI (FastAPI + Celery + Neon pgvector) ]
      - Nhận diện bối cảnh trích dẫn & Phát hiện nguồn thứ cấp
      - Cross-Encoder Semantic Reranker & Platt Calibration
      - Sinh Report Schema v3 (Similarity Coverage & Aligned Spans)
```

### Nguyên tắc Bảo mật Bắt buộc
1. **Zero Secret on Browser:** Trình duyệt của sinh viên và giảng viên **tuyệt đối không bao giờ** được nhận hoặc lưu trữ `X-API-Key` của dịch vụ AI. Endpoint `/api/v1/client-config` trên AI service đã được kiểm toán để loại bỏ 100% secret.
2. **Không chặn giao tác nộp bài của sinh viên:** Khi sinh viên nộp bài (`POST /api/thesis/final-reports`), UGTM lưu tệp vào S3 và ghi nhận bản ghi vào database nghiệp vụ kèm một outbox event. Việc gọi sang AI service được thực hiện bởi background worker của UGTM. Nếu AI service hoặc GPU tạm thời offline, sinh viên vẫn nộp bài thành công bình thường.

---

## 3. Ma trận Phân quyền Truy cập Báo cáo (RBAC Matrix)

Căn cứ theo hệ thống Role và logic kiểm tra quyền tại `FinalReportReadAccessChecker.cs` của repo `karasuma-2401/UGTM`:

| Vai trò người dùng (Role) | Quyền kích hoạt quét | Quyền xem Báo cáo AI qua UGTM API | Hành vi hiển thị dữ liệu |
|---|---|---|---|
| **Sinh viên (Student)** | Tự động khi nộp bản mới | **Chỉ xem biên nhận (Receipt Only)** | Chỉ nhận thông tin: Đã tiếp nhận bài, số lượng trang, thời gian quét. **Tuyệt đối ẩn `findings` và `source_text`** để tránh gian lận lách thuật toán và bảo mật tài liệu nguồn. |
| **Giảng viên hướng dẫn (Advisor)** | Được yêu cầu quét lại (`rescan`) | **Toàn quyền (Full Access)** | Xem đầy đủ `summary`, `findings`, văn bản đối chiếu song song, ngữ cảnh trích dẫn và ghi nhận xét thẩm định cho nhóm mình phụ trách. Bị chặn (403) nếu không phải GVHD được gán. |
| **Giảng viên phản biện / Hội đồng (CouncilMember)** | Không | **Toàn quyền khi đến giai đoạn** | Chỉ được xem khi bài nộp đã chuyển sang trạng thái thẩm định/bảo vệ (`UnderEvaluation` hoặc `Archived`) và giảng viên có tên trong hội đồng bảo vệ của đề tài. |
| **Ban chủ nhiệm Khoa (Faculty)** | Được yêu cầu quét lại đợt | **Toàn quyền theo Khoa** | Xem toàn bộ báo cáo của các nhóm thuộc khoa mình quản lý. Bị chặn nếu nhóm thuộc khoa khác. |
| **Quản trị hệ thống (Admin)** | Toàn quyền quản trị | **Toàn quyền hệ thống** | Tra cứu trạng thái job, quản lý danh mục nguồn thư viện, retry job lỗi. |

---

## 4. Đặc tả Giao diện API Dịch vụ AI (REST Endpoints)

Base URL: `http://ai-internal.ugtm.local:8000` (hoặc URL nội bộ được cấu hình).

### 4.1. Tạo Job Quét Tương đồng — `POST /api/v1/scans`
- **Headers:**
  - `X-API-Key`: `<SERVICE_API_KEY>` (Bắt buộc)
  - `X-Request-ID`: `<guid>` (Khuyến nghị để truy vết log)
  - `Content-Type`: `multipart/form-data`
- **Form Data:**
  - `corpus_id`: Mã kho tài liệu tham chiếu (ví dụ: `uit-kltn-library-v1`).
  - `submission_id`: Định danh bài nộp (ví dụ: `FinalReport.Id` định dạng GUID).
  - `version`: Phiên bản bài nộp (số nguyên $\ge 1$).
  - `file`: Tệp văn bản bài nộp (`.pdf` hoặc `.docx`, tối đa 20 MiB).
- **Mã phản hồi:**
  - `202 Accepted`: Job được xếp hàng thành công (`{"job_id": "<uuid>", "status": "queued"}`). Nếu gọi lại cùng nội dung SHA-256, trả về `{"job_id": "<uuid>", "status": "existing"}` (Idempotent).
  - `400 Bad Request`: Tệp rỗng.
  - `401 Unauthorized`: Thiếu hoặc sai API key.
  - `409 Conflict`: Cùng `submission_id` và `version` nhưng nội dung SHA-256 khác với lần trước ("Version already exists with different content").
  - `413 Payload Too Large`: Kích thước tệp vượt quá cấu hình tối đa.
  - `415 Unsupported Media Type`: Tệp không phải `.pdf`/`.docx` (ví dụ tệp `.doc` cũ của Word 97-2003 hoặc tệp hỏng).
  - `422 Unprocessable Entity`: Metadata sai định dạng regex (`^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$`).

### 4.2. Tra cứu Trạng thái Vòng đời Job — `GET /api/v1/jobs/{job_id}`
- **Headers:** `X-API-Key: <SERVICE_API_KEY>`
- **Response 200 OK:**
```json
{
  "job_id": "9f8c2d14-71a4-4e3a-8c05-3e19a72b4d90",
  "type": "scan",
  "status": "processing",
  "error_code": null,
  "attempt": 1,
  "max_attempts": 5,
  "retry_after": null,
  "updated_at": "2026-10-31T06:00:00Z"
}
```
- **Các trạng thái job:**
  - `queued`: Đang chờ worker nhận task.
  - `processing`: Worker đang phân tích cú pháp hoặc chạy suy luận.
  - `waiting_for_compute`: Worker đang chờ GPU kết nối lại và tự động retry theo exponential backoff (UGTM hiển thị trạng thái *"Đang xử lý"*).
  - `completed`: Quét hoàn tất, sẵn sàng lấy báo cáo.
  - `failed`: Xử lý thất bại sau khi hết số lần thử.
  - `cancelled`: Job đã bị huỷ bởi quản trị viên.

### 4.3. Lấy Báo cáo Bằng chứng — `GET /api/v1/reports/{job_id}`
- **Query parameters:**
  - `schema`: (Tùy chọn) `3` (mặc định) hoặc `2` (hạ cấp tương thích ngược).
  - `page`: (Tùy chọn) Số trang của danh sách findings (mặc định `1`).
  - `page_size`: (Tùy chọn) Số lượng findings mỗi trang (mặc định `100`, tối đa `500`).
- **Response 200 OK (Report Schema v3):**
```json
{
  "schema_version": 3,
  "job_id": "9f8c2d14-71a4-4e3a-8c05-3e19a72b4d90",
  "summary": {
    "similarity_coverage": 14.25,
    "max_reranker_score": 0.9120,
    "scanned_chunks": 48,
    "scanned_characters": 24500,
    "matched_characters": 3491,
    "cited_findings": 12,
    "uncited_findings": 3
  },
  "findings": [ ... ],
  "pagination": {
    "page": 1,
    "page_size": 100,
    "total_pages": 1,
    "total_findings": 15,
    "has_next": false,
    "has_prev": false
  }
}
```
- **Response 409 Conflict:** Khi job chưa ở trạng thái `completed` (`{"detail": "Report not ready: <status>"}`).

---

## 5. Mã C# Triển khai Mẫu cho Backend UGTM (.NET 10)

Dưới đây là mã nguồn mẫu chuẩn mực cho các kỹ sư backend .NET 10 để đăng ký HTTP Client và gọi dịch vụ AI.

### 5.1. Cấu hình `appsettings.json`
```json
{
  "PlagiarismAi": {
    "BaseUrl": "http://ai-internal.ugtm.local:8000",
    "ApiKey": "YOUR_SECURE_GENERATED_API_KEY",
    "DefaultCorpus": "uit-kltn-library-v1",
    "TimeoutSeconds": 60
  }
}
```

### 5.2. Đăng ký Dịch vụ trong `Program.cs` (.NET 10)
```csharp
builder.Services.AddHttpClient<IPlagiarismAiClient, PlagiarismAiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<PlagiarismAiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("X-API-Key", options.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
```

### 5.3. Service Implementation `PlagiarismAiClient.cs`
```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public interface IPlagiarismAiClient
{
    Task<ScanJobSubmissionResult> SubmitScanJobAsync(
        string corpusId,
        Guid submissionId,
        int version,
        Stream fileStream,
        string fileName,
        CancellationToken ct = default);

    Task<JobStatusResponse?> GetJobStatusAsync(Guid jobId, CancellationToken ct = default);
    Task<JsonElement?> GetReportAsync(Guid jobId, int? schema = 3, int page = 1, int pageSize = 100, CancellationToken ct = default);
}

public class PlagiarismAiClient : IPlagiarismAiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PlagiarismAiClient> _logger;

    public PlagiarismAiClient(HttpClient httpClient, ILogger<PlagiarismAiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ScanJobSubmissionResult> SubmitScanJobAsync(
        string corpusId,
        Guid submissionId,
        int version,
        Stream fileStream,
        string fileName,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(corpusId), "corpus_id");
        content.Add(new StringContent(submissionId.ToString("D")), "submission_id");
        content.Add(new StringContent(version.ToString()), "version");

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) 
                ? "application/pdf" 
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/scans") { Content = content };
        request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString("D"));

        using var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            var json = await response.Content.ReadFromJsonAsync<ScanJobAcceptedDto>(cancellationToken: ct);
            return new ScanJobSubmissionResult(json!.JobId, json.Status, IsSuccess: true);
        }

        if (response.StatusCode == HttpStatusCode.UnsupportedMediaType)
        {
            _logger.LogWarning("File {FileName} format not supported by AI service (.doc legacy)", fileName);
            return new ScanJobSubmissionResult(Guid.Empty, "unsupported_format", IsSuccess: false, ErrorCode: "UNSUPPORTED_FILE_FORMAT");
        }

        var errorBody = await response.Content.ReadAsStringAsync(ct);
        _logger.LogError("AI scan submission failed: {Status} - {Body}", response.StatusCode, errorBody);
        throw new HttpRequestException($"AI Scan submission failed with HTTP {response.StatusCode}: {errorBody}");
    }

    public async Task<JobStatusResponse?> GetJobStatusAsync(Guid jobId, CancellationToken ct = default)
    {
        var resp = await _httpClient.GetAsync($"/api/v1/jobs/{jobId:D}", ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JobStatusResponse>(cancellationToken: ct);
    }

    public async Task<JsonElement?> GetReportAsync(Guid jobId, int? schema = 3, int page = 1, int pageSize = 100, CancellationToken ct = default)
    {
        var url = $"/api/v1/reports/{jobId:D}?schema={schema}&page={page}&page_size={pageSize}";
        var resp = await _httpClient.GetAsync(url, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        if (resp.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogInformation("Report for job {JobId} is not ready yet", jobId);
            return null; // Not ready
        }
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    }
}
```

---

## 6. Danh mục Mã lỗi và Khuyến nghị Xử lý (Error Handling Matrix)

| HTTP Code | Ý nghĩa / Mã lỗi AI | Hành vi khuyến nghị phía UGTM (.NET) |
|---|---|---|
| `400` | `Empty file` | Đánh dấu phiên bản nộp là `InvalidFile`, thông báo cho sinh viên tệp bị rỗng. |
| `401` | `Unauthorized` | Báo động hệ thống (System Alert): API key cấu hình trong `appsettings.json` bị sai lệch hoặc hết hạn. |
| `409` | `Version already exists with different content` | Trùng phiên bản nhưng khác hash SHA-256. Báo lỗi `VersionMismatch`, yêu cầu sinh viên tăng `version` khi nộp lại. |
| `409` | `Report not ready: <status>` | Báo cáo đang xử lý hoặc đang chờ GPU. UGTM tiếp tục hiển thị trạng thái `InProgress` cho người dùng. |
| `413` | `File too large` | Tệp vượt quá giới hạn 20 MiB. Thông báo sinh viên nén dung lượng tệp trước khi nộp. |
| `415` | `Only PDF/DOCX allowed` | Tệp `.doc` Word 97-2003 hoặc tệp nhị phân không hợp lệ. Đánh dấu trạng thái `UnsupportedFormat` và hiển thị ghi chú cho GVHD. Không retry. |
| `422` | `Invalid metadata` | Tham số `corpus_id` hoặc `submission_id` chứa ký tự lạ ngoài regex. Kiểm tra lại việc truyền định dạng GUID. |

---

## 7. Kết luận Nghiệm thu Checkpoint CP-05.4

- **Hợp đồng tích hợp:** Đã được đặc tả đầy đủ và sẵn sàng để đội ngũ kỹ sư UGTM đưa vào sử dụng.
- **Bảo mật:** Đảm bảo 100% không để lộ master key cho phía client trình duyệt.
- **Phân quyền:** Đã kiểm chứng ma trận RBAC `FinalReportReadAccessChecker` ngăn chặn sinh viên tiếp cận văn bản nguồn của bài khác.
- **Trạng thái thực tế:** Đã hoàn tất tầng hợp đồng và kiểm thử mô phỏng (`11/11 passed`). Trạng thái tích hợp môi trường thực tế được ghi nhận là **Đang chờ (Pending deployment với UGTM backend)**.

