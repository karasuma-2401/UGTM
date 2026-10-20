namespace UGTM.Modules.ThesisManagement.Contracts.TopicRegistrations;

public sealed record TopicMemberContractDto(
    Guid StudentId,
    Guid BatchId,
    DateTime JoinedAt);

public sealed record TopicRegistrationContractDto(
    Guid GroupId,
    Guid TopicId,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<TopicMemberContractDto> Members);

public interface IThesisRegistrationContract
{
    Task<IReadOnlyDictionary<Guid, TopicRegistrationContractDto>> GetRegistrationsByTopicIdsAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default);

    Task<TopicRegistrationContractDto?> GetRegistrationByTopicIdAsync(
        Guid topicId,
        CancellationToken cancellationToken = default);
}
