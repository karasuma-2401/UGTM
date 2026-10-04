using UGTM.Persistence.Entities.Topics;

namespace UGTM.Persistence.Entities.Councils;

public class AdvisorEvaluation
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
    public DateTime EvaluatedAt { get; set; }

    public StudentGroup Group { get; set; } = null!;
}
