using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class ReviewerEvaluation
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
    public string? RevisionFeedback { get; set; }
    public bool IsIgnoredInCalculation { get; set; }
    public string? ExclusionReason { get; set; }
    public DefenseApprovalStatus DefenseApproval { get; set; }
    public bool IsVisibleToStudent { get; set; } = false;
    public DateTime EvaluatedAt { get; set; }

    public ReviewerAssignment Assignment { get; set; } = null!;
}
