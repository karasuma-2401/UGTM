namespace UGTM.Modules.TopicCatalog.Contracts.Topics;

public sealed record TopicSummaryContractDto(
    Guid TopicId,
    Guid BatchId,
    Guid AdvisorId,
    string TitleVi,
    string? TitleEn,
    string? Description = null,
    string? Requirements = null,
    int? StudentNumber = null);

public interface ITopicSummaryReader
{
    Task<IReadOnlyDictionary<Guid, TopicSummaryContractDto>> GetByIdsAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default);
}
