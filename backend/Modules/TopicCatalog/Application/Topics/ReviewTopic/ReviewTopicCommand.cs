namespace UGTM.Modules.TopicCatalog.Application.Topics.ReviewTopic;

public sealed record ReviewTopicCommand(
    Guid TopicId,
    string Decision);
