namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class ReviewerEvaluationConfiguration : IEntityTypeConfiguration<ReviewerEvaluation>
{
    public void Configure(EntityTypeBuilder<ReviewerEvaluation> builder)
    {
        builder.HasIndex(x => x.AssignmentId)
            .HasDatabaseName("ix_reviewer_evaluation_assignment_id")
            .IsUnique();

        builder.ToTable("reviewer_evaluation", "councils", table =>
        {
            table.HasCheckConstraint("ck_reviewer_evaluation_score", "score BETWEEN 0 AND 10");
            table.HasCheckConstraint("ck_reviewer_evaluation_status", "status IS NULL OR status IN ('Draft', 'Submitted')");
            table.HasCheckConstraint("ck_reviewer_evaluation_defense_approval", "defense_approval IS NULL OR defense_approval IN ('Approved', 'Rejected')");
            table.HasCheckConstraint("ck_reviewer_evaluation_workflow", """
                is_legacy OR (
                    id <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND status IS NOT NULL
                    AND created_by IS NOT NULL AND created_by <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND evaluated_by IS NOT NULL AND evaluated_by <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND created_at_utc IS NOT NULL AND created_at_utc <= evaluated_at
                    AND (comment IS NULL OR (char_length(btrim(comment)) BETWEEN 1 AND 2000 AND comment = btrim(comment)))
                    AND (
                        (status = 'Draft' AND submitted_by IS NULL AND submitted_at_utc IS NULL AND NOT is_visible_to_student)
                        OR (status = 'Submitted' AND score IS NOT NULL AND comment IS NOT NULL AND defense_approval IS NOT NULL
                            AND submitted_by IS NOT NULL AND submitted_by = evaluated_by
                            AND submitted_at_utc IS NOT NULL AND submitted_at_utc = evaluated_at AND is_visible_to_student)
                    )
                )
                """);
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsIgnoredInCalculation).HasDefaultValue(false);
        builder.Property(x => x.ExclusionReason).HasColumnType("text");

        builder.Property(x => x.Score).HasPrecision(4, 2);

        builder.Property(x => x.Comment)
            .HasColumnType("text");

        builder.Property(x => x.Status).HasMaxLength(50);
        builder.Property(x => x.IsLegacy).HasDefaultValue(true);
        builder.Property(x => x.Version).IsRowVersion();
        builder.Ignore(x => x.IsLocked);

        builder.Property(x => x.IsVisibleToStudent)
            .HasDefaultValue(false)
            .HasSentinel(false);

        builder.Property(x => x.EvaluatedAt)
            .IsRequired();

        builder.HasOne(x => x.Assignment)
            .WithOne(a => a.Evaluation)
            .HasForeignKey<ReviewerEvaluation>(x => x.AssignmentId)
            .HasConstraintName("fk_reviewer_evaluation_reviewer_assignment_assignment_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
