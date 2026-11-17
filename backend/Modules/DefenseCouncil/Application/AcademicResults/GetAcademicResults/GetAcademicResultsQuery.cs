namespace UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResults;

public sealed record GetAcademicResultsQuery(
    Guid BatchId,
    string Phase,
    string? Keyword = null,
    string? Status = null,
    int PageIndex = 1,
    int PageSize = 50);
