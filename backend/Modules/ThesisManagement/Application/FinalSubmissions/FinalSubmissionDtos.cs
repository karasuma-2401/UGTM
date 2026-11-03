using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Application.FinalSubmissions;

public sealed record FinalSubmissionFileDto(Guid FileId, string FileRole, string FileName, long FileSize,
    string ContentType, string? ChecksumSha256, DateTime UploadedAt);
public sealed record FinalSubmissionReviewDto(Guid ReviewId, string Action, string Stage, Guid ActorId,
    DateTime DecidedAtUtc, string? Reason, string PreviousStatus, string ResultStatus);
public sealed record FinalSubmissionPackageDto(Guid PackageId, int VersionNo, string AbstractVi, string AbstractEn,
    string KeywordsVi, string KeywordsEn, Guid SubmittedBy, DateTime SubmittedAt,
    IReadOnlyList<FinalSubmissionFileDto> Files, IReadOnlyList<FinalSubmissionReviewDto> Reviews);
public sealed record FinalSubmissionDto(Guid SubmissionId, Guid GroupId, string Status, int CurrentVersionNo,
    string? AbstractVi, string? AbstractEn, string? KeywordsVi, string? KeywordsEn, Guid? SubmittedBy, DateTime? SubmittedAt,
    uint SubmissionVersion, Guid? AdvisorConfirmedBy, DateTime? AdvisorConfirmedAt, Guid? ArchivedConfirmedBy,
    DateTime? ArchivedConfirmedAt, string? FacultyFeedback, bool IsLegacy, IReadOnlyList<FinalSubmissionFileDto> Files);
public sealed record FinalSubmissionEligibilityDto(Guid GroupId, bool CanSubmit, bool IsBatchOpen, bool IsWithinSubmissionWindow,
    DateTime? SubmissionDeadline, bool IsCouncilDefenseCompleted, string CouncilMinuteLockStatus,
    string CurrentSubmissionStatus, uint? SubmissionVersion, IReadOnlyList<string> BlockingReasons);
public sealed record FinalSubmissionDownloadDto(Guid FileId, string FileName, string FileRole, long FileSize, string DownloadUrl, DateTime ExpiresAt);
public sealed record FinalSubmissionSummaryDto(Guid SubmissionId, Guid GroupId, Guid BatchId, string TopicTitle,
    string Status, int CurrentVersionNo, uint SubmissionVersion, DateTime? SubmittedAt, DateTime? AdvisorConfirmedAt, DateTime? ArchivedConfirmedAt);
public sealed record FinalSubmissionPageDto(IReadOnlyList<FinalSubmissionSummaryDto> Items, int TotalCount, int PageNumber, int PageSize);
public sealed record FinalSubmissionWriteResult(FinalSubmissionDto Data, int StatusCode);
public sealed record FinalSubmissionConfirmationDto(Guid SubmissionId, Guid GroupId, string Status, int CurrentVersionNo,
    uint SubmissionVersion, Guid? AdvisorConfirmedBy, DateTime? AdvisorConfirmedAt, Guid? ArchivedConfirmedBy,
    DateTime? ArchivedConfirmedAt, string? FacultyFeedback, DateTime UpdatedAt);
public sealed record FinalSubmissionPackageGraph(IReadOnlyList<FinalSubmissionPackage> Packages,
    IReadOnlyList<FinalSubmissionPackageFile> Links, IReadOnlyList<SubmissionFile> Files, IReadOnlyList<FinalSubmissionReview> Reviews);
