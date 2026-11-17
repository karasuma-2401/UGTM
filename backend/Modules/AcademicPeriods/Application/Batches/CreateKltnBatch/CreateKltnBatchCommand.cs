namespace UGTM.Modules.AcademicPeriods.Application.Batches.CreateKltnBatch;

public sealed record CreateKltnBatchCommand(
    string BatchName,
    string AcademicYear,
    int Semester,
    DateOnly StartDate,
    string TemplateCode = CreateKltnBatchHandler.DefaultTemplateCode);
