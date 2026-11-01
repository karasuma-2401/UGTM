using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Domain.Repositories;

public interface IReviewerEvaluationRepository
{
    Task<ReviewerEvaluation?> GetByAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default);
    /// <summary>Call after the assignment workflow lock, in the same coordinator transaction.</summary>
    Task<ReviewerEvaluation?> LockByAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default);
    void Add(ReviewerEvaluation evaluation);
}
