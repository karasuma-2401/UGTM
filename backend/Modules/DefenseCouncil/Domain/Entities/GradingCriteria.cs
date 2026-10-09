
namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class GradingCriteria
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string CriteriaCode { get; set; } = null!;
    public string CriteriaName { get; set; } = null!;
    public decimal MaxScore { get; set; }
    public int OrderIndex { get; set; } = 1;

}
