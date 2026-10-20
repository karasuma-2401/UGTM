namespace UGTM.Modules.TopicCatalog.Application.Topics.CreateTopic;

public sealed record CreateTopicCommand(
    Guid BatchId,
    string TitleVi,
    string? TitleEn,
    string? Description,
    string? Requirements,
    int? StudentNumber,
    bool SubmitImmediately = false,
    Guid? AdvisorId = null);
