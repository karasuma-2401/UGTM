namespace UGTM.Modules.AcademicPeriods.Application.Batches.ChangeKltnBatchStatus;

public sealed record OpenKltnBatchCommand(Guid BatchId);
public sealed record CloseKltnBatchCommand(Guid BatchId);
public sealed record ArchiveKltnBatchCommand(Guid BatchId);
