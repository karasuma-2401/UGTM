namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class EvaluationDetail
{
    public Guid Id { get; set; }
    public Guid CriteriaId { get; set; }
    public Guid EvaluationId { get; set; }
    public decimal Score { get; set; }

    public GradingCriteria Criteria { get; set; } = null!;
}
