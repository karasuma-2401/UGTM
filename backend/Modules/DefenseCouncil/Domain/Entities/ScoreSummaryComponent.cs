namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class ScoreSummaryComponent
{
    public Guid Id { get; set; }
    public Guid SummaryId { get; set; }
    public string ComponentCode { get; set; } = null!;
    public string SourceKey { get; set; } = null!;
    public decimal Score { get; set; }
    public decimal Weight { get; set; }
    public bool IsIgnoredInCalculation { get; set; }
    public string? ExclusionReason { get; set; }

    public ScoreSummary Summary { get; set; } = null!;
}
