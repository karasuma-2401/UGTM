
namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class GradingCriteria
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string CriteriaCode { get; set; } = null!;
    public string CriteriaName { get; set; } = null!;
    public string? SectionCode { get; set; }
    public string? SectionName { get; set; }
    public int? SectionOrder { get; set; }
    public string? Elo { get; set; }
    public decimal MaxScore { get; set; }
    public int OrderIndex { get; set; } = 1;

}
