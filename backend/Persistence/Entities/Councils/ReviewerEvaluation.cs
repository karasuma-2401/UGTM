using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Entities.Councils;

public class ReviewerEvaluation
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
    public DefenseApprovalStatus DefenseApproval { get; set; }
    public bool IsVisibleToStudent { get; set; } = false;
    public DateTime EvaluatedAt { get; set; }

    public ReviewerAssignment Assignment { get; set; } = null!;
}
