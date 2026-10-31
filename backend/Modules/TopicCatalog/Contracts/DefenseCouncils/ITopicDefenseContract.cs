namespace UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

public sealed record TopicDefenseDetailsContractDto(
    Guid TopicId,
    Guid BatchId,
    Guid AdvisorId,
    string TitleVi,
    string? TitleEn);

public interface ITopicDefenseContract
{
    Task<TopicDefenseDetailsContractDto?> GetTopicDetailsAsync(
        Guid topicId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, TopicDefenseDetailsContractDto>> GetTopicsDetailsAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default);
}
