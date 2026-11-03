using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;

namespace UGTM.Modules.ThesisManagement.Infrastructure.PlagiarismAI;

public sealed class PlagiarismAiClient : IPlagiarismAiClient
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
                var payload = await response.Content.ReadFromJsonAsync<ScanJobAcceptedDto>(cancellationToken: ct);
                return new ScanJobSubmissionResult(payload?.JobId ?? Guid.Empty, payload?.Status ?? "queued", IsSuccess: true);
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
            var response = await _httpClient.GetAsync($"/api/v1/jobs/{jobId:D}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JobStatusRawResponse>(cancellationToken: ct);
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
            var url = $"/api/v1/reports/{jobId:D}?schema={schema}&page={page}&page_size={pageSize}";
            var response = await _httpClient.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Conflict)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not fetch report for AI job {JobId}", jobId);
            return null;
        }
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
                var payload = await response.Content.ReadFromJsonAsync<ScanJobAcceptedDto>(cancellationToken: ct);
                return new ScanJobSubmissionResult(payload?.JobId ?? Guid.Empty, payload?.Status ?? "queued", IsSuccess: true);
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

