namespace UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;

/// <summary>
/// Immutable proof that a group is currently eligible for reviewer assignment. Consumers must
/// query this contract immediately before their own assignment write so stale approval data is
/// never treated as sufficient on its own.
/// </summary>
public sealed record ReviewerAssignmentEligibilityContractDto(
    Guid AdvisorEvaluationId,
    Guid GroupId,
    Guid FinalReportId,
    int ReportVersionNo,
    Guid SubmissionFileId,
    string SubmissionChecksumSha256,
    DateTime ApprovedAtUtc);

public interface IReviewerAssignmentEligibilityContract
{
    Task<ReviewerAssignmentEligibilityContractDto?> GetReviewerAssignmentEligibilityAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);
}
