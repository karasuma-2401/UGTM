namespace UGTM.Persistence.Entities.Academic;

public class Stage
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public int StageIndex { get; set; }
    public string StageName { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateTime DeadlineDate { get; set; }

    public KltnBatch Batch { get; set; } = null!;
}
