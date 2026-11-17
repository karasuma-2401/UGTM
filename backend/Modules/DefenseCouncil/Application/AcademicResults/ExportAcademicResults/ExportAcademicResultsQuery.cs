namespace UGTM.Modules.DefenseCouncil.Application.AcademicResults.ExportAcademicResults;

public sealed record ExportAcademicResultsQuery(
    Guid BatchId,
    string Format,
    string? Phase = "Final",
    string? Keyword = null,
    string? Status = null);
