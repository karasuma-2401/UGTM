namespace UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;

public sealed record GetMyTopicsQuery(
    string? Keyword = null,
    Guid? BatchId = null,
    string? Status = null,
    string? RegistrationStatus = null,
    Guid? AdvisorId = null,
    int PageNumber = 1,
    int PageSize = 10);
