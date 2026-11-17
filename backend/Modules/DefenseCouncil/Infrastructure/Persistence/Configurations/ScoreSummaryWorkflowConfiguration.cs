namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public sealed class ScoreSummaryRevisionConfiguration : IEntityTypeConfiguration<ScoreSummaryRevision>
{
    public void Configure(EntityTypeBuilder<ScoreSummaryRevision> b)
    {
        b.ToTable("score_summary_revision", "councils", t => t.HasCheckConstraint("ck_score_revision", "revision_no > 0 AND config_version_no > 0 AND final_score_raw BETWEEN 0 AND 10 AND final_score BETWEEN 0 AND 10 AND total_weight > 0 AND rounding_mode='AwayFromZero' AND precision=2 AND input_fingerprint ~ '^[0-9a-f]{64}$'"));
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.Id, x.SummaryId });
        b.HasIndex(x => new { x.SummaryId, x.RevisionNo }).IsUnique();
        b.HasOne<ScoreSummary>().WithMany().HasForeignKey(x => x.SummaryId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ConfigSnapshot).HasColumnType("jsonb");
        b.Property(x => x.SourceSnapshot).HasColumnType("jsonb");
        b.Property(x => x.InputFingerprint).HasMaxLength(64);
        b.Property(x => x.FinalScoreRaw).HasColumnType("numeric");
        b.Property(x => x.FinalScore).HasPrecision(4, 2);
        b.Property(x => x.TotalWeight).HasPrecision(12, 4);
        b.Property(x => x.CalculationFormula).HasColumnType("text");
        b.Property(x => x.RoundingMode).HasMaxLength(30);
    }
}
public sealed class ScoreSummaryIdempotencyConfiguration : IEntityTypeConfiguration<ScoreSummaryIdempotency>
{
    public void Configure(EntityTypeBuilder<ScoreSummaryIdempotency> b)
    {
        b.ToTable("score_summary_idempotency", "councils", t => t.HasCheckConstraint("ck_score_receipt", "operation IN ('ScoreSummary.Calculate','ScoreSummary.Publish') AND response_status=200 AND fingerprint ~ '^[0-9a-f]{64}$' AND jsonb_typeof(response_payload)='object'"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ActorId, x.Operation, x.IdempotencyKey }).IsUnique();
        b.Property(x => x.Operation).HasMaxLength(64);
        b.Property(x => x.Fingerprint).HasMaxLength(64);
        b.Property(x => x.ResponsePayload).HasColumnType("jsonb");
    }
}
