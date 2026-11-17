namespace UGTM.Modules.TopicCatalog.Contracts.Topics;

public interface ITopicSearchReader
{
    Task<IReadOnlyList<Guid>> FindIdsAsync(
        Guid? batchId,
        string? keyword,
        CancellationToken cancellationToken = default);
}
