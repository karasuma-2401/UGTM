namespace UGTM.Persistence.Entities.Councils;

public class CouncilScore
{
    public Guid Id { get; set; }
    public Guid CouncilAssignmentId { get; set; }
    public Guid CouncilMemberId { get; set; }
    public decimal Score { get; set; }
    public string? Feedback { get; set; }
    public DateTime EvaluatedAt { get; set; }

    public CouncilAssignment Assignment { get; set; } = null!;
    public CouncilMember CouncilMember { get; set; } = null!;
}
