namespace UGTM.Modules.TopicCatalog.Contracts.Reminders;

public sealed record DraftTopicReminderDto(
    Guid TopicId,
    Guid AdvisorId,
    string TitleVi);

public sealed record SubmittedTopicReminderDto(
    Guid TopicId,
    Guid AdvisorId,
    string TitleVi);

public sealed record TopicBatchReminderSnapshotDto(
    IReadOnlyList<DraftTopicReminderDto> DraftTopics,
    IReadOnlyList<SubmittedTopicReminderDto> SubmittedTopics,
    IReadOnlyDictionary<Guid, TopicReminderSummaryDto> TopicsById);

public sealed record TopicReminderSummaryDto(
    Guid TopicId,
    Guid AdvisorId,
    string TitleVi);

public interface ITopicDeadlineSnapshotReader
{
    Task<TopicBatchReminderSnapshotDto> GetBatchSnapshotAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
}
