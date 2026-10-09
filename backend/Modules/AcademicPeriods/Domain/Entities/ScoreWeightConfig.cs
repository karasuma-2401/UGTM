using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Domain.Entities;

public class ScoreWeightConfig
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public bool IsActive { get; set; } = true;
    public ScoreCalculationMethod CalculationMethod { get; set; } = ScoreCalculationMethod.IntegerCoefficient;
    public int VersionNo { get; set; } = 1;
    public DateTime CreatedAt { get; set; }

    public KltnBatch Batch { get; set; } = null!;
    public ICollection<ScoreComponentConfig> Components { get; set; } = [];
}
