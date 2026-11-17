namespace UGTM.Modules.TopicCatalog.Application.Topics.PublishTopics;

public sealed record PublishTopicsCommand(
    Guid BatchId,
    IReadOnlyCollection<Guid>? TopicIds = null);
