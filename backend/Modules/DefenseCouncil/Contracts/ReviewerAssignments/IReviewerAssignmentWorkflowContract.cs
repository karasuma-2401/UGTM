namespace UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;

public enum ReviewerAssignmentEligibilityOutcome
{
    Succeeded,
    GroupNotFound,
    ContextInvalid,
    NotEligible,
    BatchNotOpen,
    ReviewerNotFound,
    ReviewerNotEligible,
    AdvisorConflict,
    AlreadyAssigned
}

public sealed record ReviewerAssignmentBindingContractDto(
    Guid GroupId, Guid TopicId, Guid BatchId, Guid AdvisorId, Guid AdvisorEvaluationId,
    Guid FinalReportId, int ReportVersionNo, Guid SubmissionFileId, string SubmissionChecksumSha256,
    DateTime ApprovedAtUtc);

public sealed record ReviewerAssignmentEligibilityResultContractDto(
    ReviewerAssignmentEligibilityOutcome Outcome, ReviewerAssignmentBindingContractDto? Binding = null);

public sealed record ReviewerAssignmentReportAccessContractDto(
    Guid AssignmentId, uint AssignmentVersion, Guid ReviewerId, ReviewerAssignmentBindingContractDto Binding);

public interface IReviewerAssignmentWorkflowContract
{
    /// <summary>Requires coordinator transaction. Locks all creation guards; does not create a row.</summary>
    Task<ReviewerAssignmentEligibilityResultContractDto> LockForCreationAsync(Guid groupId, Guid reviewerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only authorization projection for an exact file, never a proof for a later write.
    /// Contains no score, evaluation comment, object key or download URL.
    /// </summary>
    Task<ReviewerAssignmentReportAccessContractDto?> GetActiveReportAccessAsync(Guid reviewerId, Guid fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// SCRUM-105 must call this INSIDE coordinator transaction and insert its evaluation before
    /// that transaction commits. Locks the active assignment so cancellation cannot race the insert.
    /// A null result must abort the workflow, not be treated as a previous approval snapshot.
    /// </summary>
    Task<ReviewerAssignmentReportAccessContractDto?> LockForEvaluationAsync(Guid assignmentId, Guid reviewerId,
        CancellationToken cancellationToken = default);
}
