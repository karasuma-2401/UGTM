using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class FinalReportPlagiarismScan
{
    private FinalReportPlagiarismScan() { }

    private FinalReportPlagiarismScan(
        Guid id,
        Guid finalReportId,
        Guid submissionFileId,
        int versionNo,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty || finalReportId == Guid.Empty || submissionFileId == Guid.Empty)
        {
            throw new ArgumentException("Scan, report, and submission file identifiers must be nonempty.");
        }

        if (versionNo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNo), "Version number must be positive.");
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Created time must be UTC.", nameof(createdAtUtc));
        }

        Id = id;
        FinalReportId = finalReportId;
        SubmissionFileId = submissionFileId;
        VersionNo = versionNo;
        Status = PlagiarismScanStatus.PendingSubmission;
        CreatedAt = createdAtUtc;
        AttemptCount = 0;
    }

    public Guid Id { get; private set; }
    public Guid FinalReportId { get; private set; }
    public Guid SubmissionFileId { get; private set; }
    public int VersionNo { get; private set; }
    public Guid? JobId { get; private set; }
    public PlagiarismScanStatus Status { get; private set; }
    public double? SimilarityCoverage { get; private set; }
    public double? MaxRerankerScore { get; private set; }
    public int? ScannedChunks { get; private set; }
    public int? ScannedCharacters { get; private set; }
    public int? MatchedCharacters { get; private set; }
    public int? CitedFindingsCount { get; private set; }
    public int? UncitedFindingsCount { get; private set; }
    public int? SuspectedFindingsCount { get; private set; }
    public string? RawReportJson { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastPolledAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? NextCheckAfterUtc { get; private set; }
    public uint Version { get; private set; }

    public FinalReport FinalReport { get; private set; } = null!;
    public SubmissionFile SubmissionFile { get; private set; } = null!;

    public static FinalReportPlagiarismScan Create(
        Guid id,
        Guid finalReportId,
        Guid submissionFileId,
        int versionNo,
        DateTime createdAtUtc)
        => new(id, finalReportId, submissionFileId, versionNo, createdAtUtc);

    public void MarkQueued(Guid jobId, DateTime nowUtc)
    {
        JobId = jobId;
        Status = PlagiarismScanStatus.Queued;
        LastPolledAt = nowUtc;
        ErrorCode = null;
        ErrorMessage = null;
    }

    public void MarkProcessing(DateTime nowUtc)
    {
        Status = PlagiarismScanStatus.Processing;
        LastPolledAt = nowUtc;
    }

    public void MarkWaitingForCompute(DateTime nowUtc, DateTime nextCheckUtc, string? note = null)
    {
        Status = PlagiarismScanStatus.WaitingForCompute;
        LastPolledAt = nowUtc;
        NextCheckAfterUtc = nextCheckUtc;
        ErrorMessage = note ?? "Đang chờ tài nguyên GPU xử lý suy luận (Google Colab).";
    }

    public void MarkComputeUnavailable(DateTime nowUtc, string? errorCode, string? message)
    {
        Status = PlagiarismScanStatus.ComputeUnavailable;
        LastPolledAt = nowUtc;
        ErrorCode = errorCode ?? "GPU_UNAVAILABLE_EXHAUSTED";
        ErrorMessage = message ?? "Tài nguyên tính toán GPU (Google Colab) đang tạm dừng. Tác vụ sẽ tự động tiếp tục khi GPU sẵn sàng.";
    }

    public void MarkCompleted(
        double similarityCoverage,
        double maxRerankerScore,
        int? scannedChunks,
        int? scannedCharacters,
        int? matchedCharacters,
        int? citedFindings,
        int? uncitedFindings,
        int? suspectedFindings,
        string reportJson,
        DateTime nowUtc)
    {
        Status = PlagiarismScanStatus.Completed;
        SimilarityCoverage = similarityCoverage;
        MaxRerankerScore = maxRerankerScore;
        ScannedChunks = scannedChunks;
        ScannedCharacters = scannedCharacters;
        MatchedCharacters = matchedCharacters;
        CitedFindingsCount = citedFindings;
        UncitedFindingsCount = uncitedFindings;
        SuspectedFindingsCount = suspectedFindings;
        RawReportJson = reportJson;
        CompletedAt = nowUtc;
        LastPolledAt = nowUtc;
        ErrorCode = null;
        ErrorMessage = null;
        NextCheckAfterUtc = null;
    }

    public void MarkFailed(string errorCode, string message, DateTime nowUtc)
    {
        Status = PlagiarismScanStatus.Failed;
        ErrorCode = errorCode;
        ErrorMessage = message;
        LastPolledAt = nowUtc;
        CompletedAt = nowUtc;
        NextCheckAfterUtc = null;
    }

    public void MarkUnsupportedFormat(string message, DateTime nowUtc)
    {
        Status = PlagiarismScanStatus.UnsupportedFormat;
        ErrorCode = "UNSUPPORTED_FILE_FORMAT";
        ErrorMessage = message;
        LastPolledAt = nowUtc;
        CompletedAt = nowUtc;
        NextCheckAfterUtc = null;
    }

    public void RecordPollAttempt(DateTime nowUtc, DateTime? nextCheckUtc = null)
    {
        AttemptCount++;
        LastPolledAt = nowUtc;
        NextCheckAfterUtc = nextCheckUtc;
    }

    public void ResetForRetry(DateTime nowUtc)
    {
        Status = PlagiarismScanStatus.PendingSubmission;
        JobId = null;
        ErrorCode = null;
        ErrorMessage = null;
        LastPolledAt = nowUtc;
        NextCheckAfterUtc = null;
        AttemptCount = 0;
        SimilarityCoverage = null;
        MaxRerankerScore = null;
        ScannedChunks = null;
        ScannedCharacters = null;
        MatchedCharacters = null;
        CitedFindingsCount = null;
        UncitedFindingsCount = null;
        SuspectedFindingsCount = null;
        RawReportJson = null;
        CompletedAt = null;
    }
}

