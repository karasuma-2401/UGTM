using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Domain.Repositories;

public interface IReviewerAssignmentRepository
{
    Task<ReviewerAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReviewerAssignment?> GetActiveByGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReviewerAssignment>> GetActiveByReviewerAndFileAsync(Guid reviewerId, Guid fileId,
        CancellationToken cancellationToken = default);
    Task<ReviewerAssignment?> LockByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasEvaluationAsync(Guid assignmentId, CancellationToken cancellationToken = default);
    void Add(ReviewerAssignment assignment);
}
