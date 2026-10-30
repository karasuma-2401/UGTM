namespace UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

public sealed record ReviewerAssignmentTopicContractDto(Guid TopicId, Guid BatchId, Guid AdvisorId,
    string TitleVi, string? TitleEn);

public interface IReviewerAssignmentTopicReader
{
    Task<IReadOnlyList<ReviewerAssignmentTopicContractDto>> GetManyAsync(IReadOnlyCollection<Guid> topicIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> FindIdsAsync(Guid? batchId, string? keyword, CancellationToken cancellationToken = default);
    Task<ReviewerAssignmentTopicContractDto?> GetAsync(Guid topicId, CancellationToken cancellationToken = default);

    /// <summary>Requires the caller's shared transaction; locks the current advisor and batch binding.</summary>
    Task<ReviewerAssignmentTopicContractDto?> LockAsync(Guid topicId, CancellationToken cancellationToken = default);
}
