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
    public bool IsLegacy { get; set; } = true;
    public uint Version { get; set; }
    public Guid? ConfigId { get; set; }
    public Guid? CalculatedBy { get; set; }
    public Guid? ActiveRevisionId { get; set; }
    public Guid? PublishedRevisionId { get; set; }
    public string? InputFingerprint { get; set; }
    public string? LegacySnapshot { get; set; }

    public void ApplyCalculation(ScoreSummaryRevision revision)
    {
        if (Status == ScoreSummaryStatus.PublishedToStudent) throw new InvalidOperationException("Published scores are immutable.");
        if (revision.SummaryId != Id || revision.GroupId != GroupId || revision.TotalWeight <= 0 || revision.FinalScore is < 0 or > 10)
            throw new ArgumentException("Invalid calculation revision.", nameof(revision));
        IsLegacy = false;
        ActiveRevisionId = revision.Id;
        ConfigId = revision.ConfigId;
        ConfigVersionNo = revision.ConfigVersionNo;
        CalculatedBy = revision.CalculatedBy;
        CalculatedAt = revision.CalculatedAtUtc;
        CalculationConfigSnapshot = revision.ConfigSnapshot;
        InputFingerprint = revision.InputFingerprint;
        FinalScore = revision.FinalScore;
        TotalWeight = revision.TotalWeight;
        CalculationFormula = revision.CalculationFormula;
        Status = ScoreSummaryStatus.Calculated;
        PublishedRevisionId = PublishedBy = null;
        PublishedAt = null;
    }

    public ICollection<ScoreSummaryComponent> Components { get; set; } = [];
}
