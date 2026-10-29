namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.GetMidtermReportReviewQueue;

public sealed record GetMidtermReportReviewQueueQuery(
    Guid? BatchId = null,
    string? Status = null,
    string? Keyword = null,
    int PageNumber = 1,
    int PageSize = 20);
