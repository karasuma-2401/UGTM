namespace UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

/// <summary>
/// Represents the advisor currently assigned to a topic.
/// </summary>
public sealed record TopicAdvisorAssignmentContractDto(
    Guid TopicId,
    Guid AdvisorId);

/// <summary>
/// Read-only cross-module contract for resolving a topic's assigned advisor.
/// </summary>
public interface ITopicAdvisorAssignmentContract
{
    Task<TopicAdvisorAssignmentContractDto?> GetByTopicIdAsync(
        Guid topicId,
        CancellationToken cancellationToken = default);
}
