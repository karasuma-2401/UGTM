using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Domain.Repositories;

public interface IReviewerAssignmentIdempotencyRepository
{
    /// <summary>Acquire first in the shared transaction, before checking a receipt or business guards.</summary>
    Task LockRequestAsync(Guid actorId, string operation, Guid key, CancellationToken cancellationToken = default);
    Task<ReviewerAssignmentIdempotencyRecord?> GetAsync(Guid actorId, string operation, Guid key,
        CancellationToken cancellationToken = default);
    void Add(ReviewerAssignmentIdempotencyRecord record);
}
