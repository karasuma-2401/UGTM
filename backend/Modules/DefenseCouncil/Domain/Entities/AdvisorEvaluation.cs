
namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class AdvisorEvaluation
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public bool? AllowsReviewerAssignment { get; set; }
    public bool IsIgnoredInCalculation { get; set; }
    public string? ExclusionReason { get; set; }

}
