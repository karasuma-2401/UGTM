namespace UGTM.Modules.ThesisManagement.Contracts.AdvisorEvaluations;

/// <summary>
/// Immutable details of the InitialFinal report version that is locked for advisor evaluation.
/// This contract deliberately contains no score or advisor-decision data.
/// </summary>
public sealed record AdvisorEvaluationLockedFinalReportContractDto(
    Guid FinalReportId,
    Guid GroupId,
    int ReportVersionNo,
    Guid SubmissionFileId,
    string SubmissionChecksumSha256);

/// <summary>
/// Read-only report details used to authorize an advisor-evaluation request and to validate it
/// before the locked-report method marks report and group concurrency tokens for saving.
/// </summary>
public sealed record AdvisorEvaluationFinalReportContextContractDto(
    Guid FinalReportId,
    Guid GroupId,
    Guid TopicId,
    bool GroupExists,
    bool IsInitialFinal,
    bool IsUnderEvaluation,
    bool IsGroupFinalReportSubmitted,
    int CurrentVersionNo,
    Guid? SubmissionFileId,
    string? SubmissionChecksumSha256);

public enum AdvisorEvaluationGroupReadinessOutcome
{
    Succeeded,
    FinalReportNotFound,
    GroupNotFound,
    InvalidFinalReportState,
    ReportVersionMismatch,
    InvalidGroupState
}

public sealed record AdvisorEvaluationGroupReadinessResultContractDto(
    AdvisorEvaluationGroupReadinessOutcome Outcome,
    Guid? GroupId = null);

/// <summary>
/// Audit details for a decision that controls whether a group can move to reviewer assignment.
/// The ThesisManagement implementation records this in its own outbox while the caller's
/// cross-module transaction is still open.
/// </summary>
public sealed record AdvisorEvaluationDecisionAuditContractDto(
    Guid EvaluationId,
    Guid GroupId,
    Guid FinalReportId,
    int ReportVersionNo,
    uint EvaluationVersion,
    bool AllowsReviewerAssignment,
    string? RejectionReason,
    Guid DecidedBy,
    DateTime DecidedAtUtc);

/// <summary>
/// Cross-module boundary for binding an advisor evaluation to the locked report and moving a
/// group to reviewer-assignment readiness. Mutation methods enlist changes in the caller's
/// advisor-workflow transaction; they never save independently.
/// </summary>
public interface IAdvisorEvaluationWorkflowContract
{
    Task<AdvisorEvaluationFinalReportContextContractDto?> GetFinalReportContextAsync(
        Guid finalReportId,
        CancellationToken cancellationToken = default);

    Task<AdvisorEvaluationLockedFinalReportContractDto?> GetLockedInitialFinalReportAsync(
        Guid finalReportId,
        CancellationToken cancellationToken = default);

    Task<AdvisorEvaluationGroupReadinessResultContractDto> TryMakeGroupReadyForReviewerAssignmentAsync(
        Guid groupId,
        Guid finalReportId,
        int expectedReportVersionNo,
        CancellationToken cancellationToken = default);

    void AddDecisionAudit(AdvisorEvaluationDecisionAuditContractDto decision);

    Task<bool> IsGroupReadyForReviewerAssignmentAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);
}
