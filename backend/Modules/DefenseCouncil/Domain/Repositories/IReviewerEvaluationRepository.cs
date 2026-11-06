using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Domain.Repositories;

public interface IReviewerEvaluationRepository
{
    Task<ReviewerEvaluation?> GetByAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default);
    /// <summary>Call after the assignment workflow lock, in the same coordinator transaction.</summary>
    Task<ReviewerEvaluation?> LockByAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default);
    void Add(ReviewerEvaluation evaluation);

    Task<IReadOnlyList<GradingCriteria>> GetGradingCriteriaByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationDetail>> GetEvaluationDetailsByEvaluationIdAsync(Guid evaluationId, CancellationToken cancellationToken = default);
    Task SaveEvaluationDetailsAsync(Guid evaluationId, IEnumerable<EvaluationDetail> details, CancellationToken cancellationToken = default);
}
