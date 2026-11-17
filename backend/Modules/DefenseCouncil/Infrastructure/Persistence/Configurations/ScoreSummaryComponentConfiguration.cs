namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class ScoreSummaryComponentConfiguration : IEntityTypeConfiguration<ScoreSummaryComponent>
{
    public void Configure(EntityTypeBuilder<ScoreSummaryComponent> builder)
    {
        builder.HasIndex(x => new { x.SummaryId, x.ComponentCode, x.SourceKey })
            .HasDatabaseName("ix_score_summary_component_summary_id_component_code_source_key")
            .IsUnique().HasFilter("revision_id IS NULL");
        builder.HasIndex(x => new { x.RevisionId, x.ComponentCode, x.SourceKey }).IsUnique().HasFilter("revision_id IS NOT NULL");

        builder.ToTable("score_summary_component", "councils", table =>
        {
            table.HasCheckConstraint("ck_summary_component_score", "score BETWEEN 0 AND 10");
            table.HasCheckConstraint("ck_summary_component_weight", "weight >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ComponentCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceKey).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Score).HasColumnType("numeric");
        builder.Property(x => x.Source).HasMaxLength(30);
        builder.Property(x => x.ComponentName).HasMaxLength(200);
        builder.Property(x => x.SourceReferences).HasColumnType("jsonb");
        builder.HasOne<ScoreSummaryRevision>().WithMany().HasForeignKey(x => new { x.RevisionId, x.SummaryId })
            .HasPrincipalKey(x => new { x.Id, x.SummaryId }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Weight).HasPrecision(9, 4);
        builder.HasOne(x => x.Summary).WithMany(x => x.Components)
            .HasForeignKey(x => x.SummaryId)
            .HasConstraintName("fk_score_summary_component_score_summaries_summary_id").OnDelete(DeleteBehavior.Restrict);
    }
}
