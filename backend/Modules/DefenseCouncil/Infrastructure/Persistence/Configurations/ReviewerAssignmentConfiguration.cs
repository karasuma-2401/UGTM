namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class ReviewerAssignmentConfiguration : IEntityTypeConfiguration<ReviewerAssignment>
{
    public void Configure(EntityTypeBuilder<ReviewerAssignment> builder)
    {
        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ux_reviewer_assignment_active_group")
            .HasFilter("is_active = true")
            .IsUnique();

        builder.HasIndex(x => new { x.GroupId, x.AssignedAt })
            .HasDatabaseName("ix_reviewer_assignment_group_id_assigned_at");

        builder.HasIndex(x => x.ReviewerId)
            .HasDatabaseName("ix_reviewer_assignment_reviewer_id");

        builder.HasIndex(x => x.AssignedBy)
            .HasDatabaseName("ix_reviewer_assignment_assigned_by");

        builder.ToTable("reviewer_assignment", "councils", table =>
        {
            table.HasCheckConstraint("ck_reviewer_assignment_workflow_binding", """
                is_legacy OR (
                    id <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND group_id <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND reviewer_id <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND assigned_by <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND advisor_id_at_assignment IS NOT NULL
                    AND advisor_id_at_assignment <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND reviewer_id <> advisor_id_at_assignment
                    AND advisor_evaluation_id IS NOT NULL
                    AND advisor_evaluation_id <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND final_report_id IS NOT NULL
                    AND final_report_id <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND report_version_no IS NOT NULL AND report_version_no > 0
                    AND submission_file_id IS NOT NULL
                    AND submission_file_id <> '00000000-0000-0000-0000-000000000000'::uuid
                    AND submission_checksum_sha256 IS NOT NULL
                    AND submission_checksum_sha256 ~ '^[0-9a-f]{64}$'
                    AND assignment_order = 1
                )
                """);
            table.HasCheckConstraint("ck_reviewer_assignment_cancellation", """
                (is_active AND cancelled_by IS NULL AND cancelled_at IS NULL AND cancellation_reason IS NULL)
                OR (NOT is_active AND (
                    (is_legacy AND cancelled_by IS NULL AND cancelled_at IS NULL AND cancellation_reason IS NULL)
                    OR (cancelled_by IS NOT NULL
                        AND cancelled_by <> '00000000-0000-0000-0000-000000000000'::uuid
                        AND cancelled_at IS NOT NULL AND cancelled_at >= assigned_at
                        AND cancellation_reason IS NOT NULL
                        AND char_length(btrim(cancellation_reason)) BETWEEN 1 AND 1000
                        AND cancellation_reason = btrim(cancellation_reason))
                ))
                """);
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AssignmentOrder).HasDefaultValue(1).HasSentinel(1);
        builder.Property(x => x.IsActive).HasDefaultValue(true).HasSentinel(true);
        builder.Property(x => x.IsLegacy).HasDefaultValue(true);
        builder.Property(x => x.SubmissionChecksumSha256).HasMaxLength(64);
        builder.Property(x => x.CancellationReason).HasColumnType("text");
        builder.Property(x => x.Version).IsRowVersion();

        builder.Property(x => x.AssignedAt)
            .IsRequired();
    }
}
