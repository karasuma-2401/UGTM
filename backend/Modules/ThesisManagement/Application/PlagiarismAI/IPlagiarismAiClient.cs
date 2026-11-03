using System.Text.Json;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI;

public interface IPlagiarismAiClient
{
    Task<ScanJobSubmissionResult> SubmitScanJobAsync(
        string corpusId,
        Guid submissionId,
        int version,
        Stream fileStream,
        string fileName,
        CancellationToken ct = default);

    Task<PlagiarismJobStatusDto?> GetJobStatusAsync(
        Guid jobId,
        CancellationToken ct = default);

    Task<JsonElement?> GetReportRawJsonAsync(
        Guid jobId,
        int schema = 3,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default);

    Task<ScanJobSubmissionResult> IndexCorpusDocumentAsync(
        string corpusId,
        string sourceId,
        int version,
        Stream fileStream,
        string fileName,
        CancellationToken ct = default);

    Task<bool> CheckHealthAsync(CancellationToken ct = default);
}

