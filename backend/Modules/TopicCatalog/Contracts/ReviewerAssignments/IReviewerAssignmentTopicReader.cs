namespace UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

public sealed record ReviewerAssignmentTopicContractDto(Guid TopicId, Guid BatchId, Guid AdvisorId);

public interface IReviewerAssignmentTopicReader
{
    Task<ReviewerAssignmentTopicContractDto?> GetAsync(Guid topicId, CancellationToken cancellationToken = default);

    /// <summary>Requires the caller's shared transaction; locks the current advisor and batch binding.</summary>
    Task<ReviewerAssignmentTopicContractDto?> LockAsync(Guid topicId, CancellationToken cancellationToken = default);
}
