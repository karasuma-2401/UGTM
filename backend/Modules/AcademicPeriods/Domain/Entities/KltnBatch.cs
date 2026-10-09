using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Domain.Entities;

public class KltnBatch
{
    public Guid Id { get; set; }
    public string BatchName { get; set; } = null!;
    public string AcademicYear { get; set; } = null!;
    public int Semester { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Draft;
    public DateTime CreatedAt { get; set; }

    public ICollection<Stage> Stages { get; set; } = [];
    public ScoreWeightConfig? ScoreWeightConfig { get; set; }
}
