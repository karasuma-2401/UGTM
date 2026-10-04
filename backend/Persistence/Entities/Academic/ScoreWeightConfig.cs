namespace UGTM.Persistence.Entities.Academic;

public class ScoreWeightConfig
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public KltnBatch Batch { get; set; } = null!;
    public ICollection<RoleScoreWeight> RoleScoreWeights { get; set; } = [];
}
