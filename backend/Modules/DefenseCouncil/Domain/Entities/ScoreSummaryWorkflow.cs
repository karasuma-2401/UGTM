namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public sealed class ScoreSummaryRevision
{
    public Guid Id { get; set; }
    public Guid SummaryId { get; set; }
    public int RevisionNo { get; set; }
    public Guid GroupId { get; set; }
    public Guid BatchId { get; set; }
    public Guid CalculatedBy { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
    public Guid ConfigId { get; set; }
    public int ConfigVersionNo { get; set; }
    public string ConfigSnapshot { get; set; } = null!;
    public string SourceSnapshot { get; set; } = null!;
    public string InputFingerprint { get; set; } = null!;
    public decimal FinalScoreRaw { get; set; }
    public decimal FinalScore { get; set; }
    public decimal TotalWeight { get; set; }
    public string CalculationFormula { get; set; } = null!;
    public string RoundingMode { get; set; } = "AwayFromZero";
    public int Precision { get; set; } = 2;
}

public sealed class ScoreSummaryIdempotency
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public string Operation { get; set; } = null!;
    public Guid IdempotencyKey { get; set; }
    public string Fingerprint { get; set; } = null!;
    public int ResponseStatus { get; set; } = 200;
    public string ResponsePayload { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
