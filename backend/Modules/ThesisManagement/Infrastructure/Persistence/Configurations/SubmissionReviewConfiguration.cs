namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public sealed class SubmissionReviewConfiguration : IEntityTypeConfiguration<SubmissionReview>
{
    public void Configure(EntityTypeBuilder<SubmissionReview> builder)
    {
        builder.ToTable("submission_review", "thesis", table =>
        {
            table.HasCheckConstraint("ck_submission_review_version_no", "version_no > 0");
        });

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.GateType, x.SubmissionId, x.VersionNo })
            .HasDatabaseName("ix_submission_review_gate_submission_version")
            .IsUnique();
        builder.HasIndex(x => x.ReviewedBy)
            .HasDatabaseName("ix_submission_review_reviewed_by");

        builder.Property(x => x.Feedback).HasColumnType("text");
        builder.Property(x => x.ReviewedAtUtc).IsRequired();
    }
}
