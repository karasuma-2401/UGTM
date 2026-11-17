namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;

public sealed record ScanJobSubmissionResult(
    Guid JobId,
    string Status,
    bool IsSuccess,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record PlagiarismJobStatusDto(
    Guid JobId,
    string Type,
    string Status,
    string? ErrorCode,
    int Attempt,
    int MaxAttempts,
    DateTime? RetryAfter,
    DateTime UpdatedAt);

