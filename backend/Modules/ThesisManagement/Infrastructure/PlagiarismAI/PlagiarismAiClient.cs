using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;

namespace UGTM.Modules.ThesisManagement.Infrastructure.PlagiarismAI;

public sealed class PlagiarismAiClient : IPlagiarismAiClient
{
    private static readonly JsonSerializerOptions AiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
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
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(corpusId), "corpus_id");
            content.Add(new StringContent(submissionId.ToString("D")), "submission_id");
            content.Add(new StringContent(version.ToString()), "version");

            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(fileName));
            content.Add(fileContent, "file", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/scans") { Content = content };
            request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString("D"));

            using var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                var payload = await response.Content.ReadFromJsonAsync<ScanJobAcceptedDto>(AiJsonOptions, ct);
                if (payload is null || payload.JobId == Guid.Empty)
                    return new ScanJobSubmissionResult(Guid.Empty, "failed", false, "INVALID_AI_RESPONSE", "Dịch vụ AI không trả mã tác vụ hợp lệ.");
                return new ScanJobSubmissionResult(payload.JobId, payload.Status ?? "queued", IsSuccess: true);
            }

            if (response.StatusCode == HttpStatusCode.UnsupportedMediaType)
            {
                _logger.LogWarning("File {FileName} is not supported by AI service (.doc legacy)", fileName);
                return new ScanJobSubmissionResult(
                    Guid.Empty,
                    "unsupported_format",
                    IsSuccess: false,
                    ErrorCode: "UNSUPPORTED_FILE_FORMAT",
                    ErrorMessage: "Dịch vụ AI chỉ hỗ trợ phân tích định dạng .pdf và .docx.");
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogWarning("Submission {SubmissionId} v{Version} conflict with different file content", submissionId, version);
                return new ScanJobSubmissionResult(
                    Guid.Empty,
                    "conflict",
                    IsSuccess: false,
                    ErrorCode: "VERSION_CONFLICT",
                    ErrorMessage: "Phiên bản bài nộp đã tồn tại với mã băm nội dung khác.");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
                return new ScanJobSubmissionResult(Guid.Empty, "unavailable", false, "AI_SERVICE_UNAVAILABLE", "Dịch vụ AI tạm thời không sẵn sàng.");

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("AI scan submission failed: {Status} - {Body}", response.StatusCode, errorBody);

            return new ScanJobSubmissionResult(
                Guid.Empty,
                "failed",
                IsSuccess: false,
                ErrorCode: $"HTTP_{(int)response.StatusCode}",
                ErrorMessage: $"Gửi quét thất bại ({response.StatusCode}): {errorBody}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP connection failure while calling AI service /api/v1/scans");
            return new ScanJobSubmissionResult(
                Guid.Empty,
                "connection_failure",
                IsSuccess: false,
                ErrorCode: "AI_SERVICE_UNAVAILABLE",
                ErrorMessage: "Không thể kết nối đến dịch vụ AI (mạng hoặc dịch vụ đang offline).");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ScanJobSubmissionResult(Guid.Empty, "timeout", false, "AI_SERVICE_UNAVAILABLE", "Dịch vụ AI không phản hồi kịp thời.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error submitting scan job to AI service");
            return new ScanJobSubmissionResult(
                Guid.Empty,
                "error",
                IsSuccess: false,
                ErrorCode: "SUBMISSION_ERROR",
                ErrorMessage: ex.Message);
        }
    }

    public async Task<PlagiarismJobStatusDto?> GetJobStatusAsync(Guid jobId, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"/api/v1/jobs/{jobId:D}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JobStatusRawResponse>(AiJsonOptions, ct);
            if (json is null) return null;

            return new PlagiarismJobStatusDto(
                json.JobId,
                json.Type ?? "scan",
                json.Status ?? "queued",
                json.ErrorCode,
                json.Attempt,
                json.MaxAttempts,
                json.RetryAfter,
                json.UpdatedAt ?? DateTime.UtcNow);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not fetch status for AI job {JobId}", jobId);
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
    }

    public async Task<bool> RetryJobAsync(Guid jobId, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.PostAsync($"/api/v1/jobs/{jobId:D}/retry", null, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not retry AI job {JobId}", jobId);
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
    }

    public async Task<JsonElement?> GetReportRawJsonAsync(
        Guid jobId,
        int schema = 3,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default)
    {
        try
        {
            JsonObject? report = null;
            JsonArray? findings = null;
            var currentPage = page;
            while (true)
            {
                var url = $"/api/v1/reports/{jobId:D}?schema={schema}&page={currentPage}&page_size={pageSize}";
                using var response = await _httpClient.GetAsync(url, ct);
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict) return null;
                response.EnsureSuccessStatusCode();
                var part = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
                if (part is null) return null;
                if (report is null)
                {
                    report = part;
                    findings = report["findings"] as JsonArray;
                }
                else if (findings is not null && part["findings"] is JsonArray nextFindings)
                {
                    foreach (var finding in nextFindings) findings.Add(finding?.DeepClone());
                }
                if (part["pagination"]?["has_next"]?.GetValue<bool>() != true) break;
                currentPage++;
            }
            if (report["summary"] is JsonObject summary && findings is not null)
                summary["findings_returned"] = findings.Count;
            return JsonSerializer.SerializeToElement(report);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not fetch report for AI job {JobId}", jobId);
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
    }

    public async Task<ScanJobSubmissionResult> IndexCorpusDocumentAsync(
        string corpusId,
        string sourceId,
        int version,
        Stream fileStream,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(corpusId), "corpus_id");
            content.Add(new StringContent(sourceId), "source_id");
            content.Add(new StringContent(version.ToString()), "version");

            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(fileName));
            content.Add(fileContent, "file", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/corpus/documents") { Content = content };
            request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString("D"));

            using var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                var payload = await response.Content.ReadFromJsonAsync<ScanJobAcceptedDto>(AiJsonOptions, ct);
                if (payload is null || payload.JobId == Guid.Empty)
                    return new ScanJobSubmissionResult(Guid.Empty, "failed", false, "INVALID_AI_RESPONSE", "Dịch vụ AI không trả mã tác vụ hợp lệ.");
                return new ScanJobSubmissionResult(payload.JobId, payload.Status ?? "queued", IsSuccess: true);
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Corpus document indexing failed: {Status} - {Body}", response.StatusCode, errorBody);

            return new ScanJobSubmissionResult(
                Guid.Empty,
                "failed",
                IsSuccess: false,
                ErrorCode: $"HTTP_{(int)response.StatusCode}",
                ErrorMessage: errorBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing corpus document {SourceId}", sourceId);
            return new ScanJobSubmissionResult(
                Guid.Empty,
                "error",
                IsSuccess: false,
                ErrorCode: "INDEX_ERROR",
                ErrorMessage: ex.Message);
        }
    }

    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await _httpClient.GetAsync("/healthz", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string GetMimeType(string fileName)
    {
        if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return "application/pdf";
        if (fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        if (fileName.EndsWith(".doc", StringComparison.OrdinalIgnoreCase))
            return "application/msword";
        return "application/octet-stream";
    }

    private sealed class ScanJobAcceptedDto
    {
        public Guid JobId { get; set; }
        public string? Status { get; set; }
    }

    private sealed class JobStatusRawResponse
    {
        public Guid JobId { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? ErrorCode { get; set; }
        public int Attempt { get; set; }
        public int MaxAttempts { get; set; }
        public DateTime? RetryAfter { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

