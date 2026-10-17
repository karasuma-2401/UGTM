namespace UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;

public sealed record GetAllTopicsQuery(
    string? Keyword = null,
    Guid? BatchId = null,
    int PageNumber = 1,
    int PageSize = 10);
