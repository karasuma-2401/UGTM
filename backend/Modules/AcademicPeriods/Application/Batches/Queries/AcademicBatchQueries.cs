namespace UGTM.Modules.AcademicPeriods.Application.Batches.Queries;

public sealed record GetKltnBatchesQuery(
    string? AcademicYear,
    int? Semester,
    string? Status,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record GetKltnBatchByIdQuery(Guid BatchId);
public sealed record GetKltnBatchTimelineQuery(Guid BatchId);
public sealed record GetCurrentStageQuery(Guid BatchId);
