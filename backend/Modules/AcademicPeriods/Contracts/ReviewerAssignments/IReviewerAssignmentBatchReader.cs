namespace UGTM.Modules.AcademicPeriods.Contracts.ReviewerAssignments;

public sealed record ReviewerAssignmentBatchContractDto(Guid BatchId, bool IsOpen);

public interface IReviewerAssignmentBatchReader
{
    Task<IReadOnlyList<ReviewerAssignmentBatchContractDto>> GetManyAsync(IReadOnlyCollection<Guid> batchIds,
        CancellationToken cancellationToken = default);
    Task<ReviewerAssignmentBatchContractDto?> GetAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>Requires the caller's shared transaction; holds the batch status stable until it ends.</summary>
    Task<ReviewerAssignmentBatchContractDto?> LockAsync(Guid batchId, CancellationToken cancellationToken = default);
}
