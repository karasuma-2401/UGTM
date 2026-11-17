namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class AdvisorEvaluationConfiguration : IEntityTypeConfiguration<AdvisorEvaluation>
{
    public void Configure(EntityTypeBuilder<AdvisorEvaluation> builder)
    {
        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_advisor_evaluation_group_id")
            .IsUnique();

        builder.HasIndex(x => x.FinalReportId)
            .HasDatabaseName("ix_advisor_evaluation_final_report_id");

        builder.HasIndex(x => x.SubmissionFileId)
            .HasDatabaseName("ix_advisor_evaluation_submission_file_id");

        builder.HasIndex(x => x.EvaluatedBy)
            .HasDatabaseName("ix_advisor_evaluation_evaluated_by");

        builder.HasIndex(x => x.DecisionBy)
            .HasDatabaseName("ix_advisor_evaluation_decision_by");

        builder.ToTable("advisor_evaluation", "councils", table =>
        {
            table.HasCheckConstraint("ck_advisor_evaluation_score", "score BETWEEN 0 AND 10");
            table.HasCheckConstraint("ck_advisor_evaluation_workflow_binding", """
                is_legacy OR (
                    final_report_id IS NOT NULL
                    AND report_version_no > 0
                    AND submission_file_id IS NOT NULL
                    AND submission_checksum_sha256 IS NOT NULL
                    AND char_length(submission_checksum_sha256) = 64
                    AND evaluated_by IS NOT NULL
                    AND comment IS NOT NULL
                    AND char_length(btrim(comment)) BETWEEN 1 AND 2000
                )
                """);
            table.HasCheckConstraint("ck_advisor_evaluation_reviewer_assignment_decision", """
                is_legacy OR (
                    (allows_reviewer_assignment IS NULL
                        AND rejection_reason IS NULL
                        AND decision_by IS NULL
                        AND decision_at IS NULL)
                    OR (allows_reviewer_assignment = FALSE
                        AND rejection_reason IS NOT NULL
                        AND char_length(btrim(rejection_reason)) BETWEEN 1 AND 1000
                        AND decision_by IS NOT NULL
                        AND decision_at IS NOT NULL)
                    OR (allows_reviewer_assignment = TRUE
                        AND rejection_reason IS NULL
                        AND decision_by IS NOT NULL
                        AND decision_at IS NOT NULL)
                )
                """);
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsIgnoredInCalculation).HasDefaultValue(false);
        builder.Property(x => x.ExclusionReason).HasColumnType("text");

        builder.Property(x => x.Score)
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasColumnType("text");

        builder.Property(x => x.EvaluatedAt)
            .IsRequired();

        builder.Property(x => x.SubmissionChecksumSha256)
            .HasMaxLength(64);

        builder.Property(x => x.RejectionReason)
            .HasColumnType("text");

        builder.Property(x => x.IsLegacy)
            .HasDefaultValue(true);

        builder.Property(x => x.Version)
            .IsRowVersion();
    }
}
