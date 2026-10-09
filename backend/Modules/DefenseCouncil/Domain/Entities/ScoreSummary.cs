using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class ScoreSummary
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public decimal? AdvisorScore { get; set; }
    public decimal? ReviewerScore { get; set; }
    public decimal? CouncilScore { get; set; }
    public decimal? FinalScore { get; set; }
    public string? CalculationFormula { get; set; }
    public int? ConfigVersionNo { get; set; }
    public decimal? TotalWeight { get; set; }
    public DateTime? CalculatedAt { get; set; }
    public string? CalculationConfigSnapshot { get; set; }
    public ScoreSummaryStatus Status { get; set; } = ScoreSummaryStatus.Pending;
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }

    public ICollection<ScoreSummaryComponent> Components { get; set; } = [];
}
