namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;

public sealed record PlagiarismSummaryDto(
    double SimilarityCoverage,
    double MaxRerankerScore,
    int ScannedChunks,
    int ScannedCharacters,
    int MatchedCharacters,
    int CitedFindings,
    int UncitedFindings,
    int? SuspectedFindings);

public sealed record PlagiarismFindingDto(
    string? PassageGroupId,
    int? SubmissionChunkNo,
    int? SubmissionPage,
    int? SubmissionStart,
    int? SubmissionEnd,
    string SubmissionText,
    string? SourceKey,
    int? SourceVersion,
    string? SourceFilename,
    int? SourcePage,
    string SourceText,
    double? RerankerScore,
    double? ClassifierScore,
    string? CitationStatus,
    string? CitationExplanation,
    bool PotentialMisattribution,
    bool ReviewRequired);

public sealed record PlagiarismReportResponseDto(
    Guid ScanId,
    Guid FinalReportId,
    int VersionNo,
    Guid? JobId,
    string Status,
    string StatusDescription,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    PlagiarismSummaryDto? Summary,
    IReadOnlyList<PlagiarismFindingDto> Findings,
    string? Disclaimer,
    bool CanRescan);

public sealed record PlagiarismReceiptResponseDto(
    Guid ScanId,
    Guid FinalReportId,
    int VersionNo,
    string FileName,
    string? ChecksumSha256,
    long FileSize,
    string Status,
    string StatusDescription,
    DateTime SubmittedAt,
    DateTime? CompletedAt,
    string Notice);

