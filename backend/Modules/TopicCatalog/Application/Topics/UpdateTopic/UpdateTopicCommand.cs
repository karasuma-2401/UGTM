namespace UGTM.Modules.TopicCatalog.Application.Topics.UpdateTopic;

public sealed record UpdateTopicCommand(
    Guid TopicId,
    string TitleVi,
    string? TitleEn,
    string? Description,
    string? Requirements,
    int? StudentNumber,
    bool SubmitImmediately = false);
