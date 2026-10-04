using UGTM.Persistence.Entities.Identity;
using UGTM.Persistence.Entities.Topics;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Entities.Councils;

public class ScoreSummary
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public decimal? AdvisorScore { get; set; }
    public decimal? ReviewerScore { get; set; }
    public decimal? CouncilScore { get; set; }
    public decimal? FinalScore { get; set; }
    public string? CalculationFormula { get; set; }
    public ScoreSummaryStatus Status { get; set; } = ScoreSummaryStatus.Pending;
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }

    public StudentGroup Group { get; set; } = null!;
    public User? PublishedByUser { get; set; }
}
