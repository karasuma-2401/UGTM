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
/// Cross-module boundary for binding an advisor evaluation to the locked report and moving a
/// group to reviewer-assignment readiness. Mutation methods enlist changes in the caller's
/// advisor-workflow transaction; they never save independently.
/// </summary>
public interface IAdvisorEvaluationWorkflowContract
{
    Task<AdvisorEvaluationLockedFinalReportContractDto?> GetLockedInitialFinalReportAsync(
        Guid finalReportId,
        CancellationToken cancellationToken = default);

    Task<AdvisorEvaluationGroupReadinessResultContractDto> TryMakeGroupReadyForReviewerAssignmentAsync(
        Guid groupId,
        Guid finalReportId,
        int expectedReportVersionNo,
        CancellationToken cancellationToken = default);

    Task<bool> IsGroupReadyForReviewerAssignmentAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);
}
