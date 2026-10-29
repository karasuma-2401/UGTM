namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusReviewQueue;

public sealed record GetSyllabusReviewQueueQuery(
    Guid? BatchId = null,
    string? Status = null,
    string? Keyword = null,
    int PageNumber = 1,
    int PageSize = 20);
