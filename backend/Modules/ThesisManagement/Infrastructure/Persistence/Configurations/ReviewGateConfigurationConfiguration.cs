namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public sealed class ReviewGateConfigurationConfiguration : IEntityTypeConfiguration<ReviewGateConfiguration>
{
    public void Configure(EntityTypeBuilder<ReviewGateConfiguration> builder)
    {
        builder.ToTable("review_gate_configuration", "thesis", table =>
        {
            table.HasCheckConstraint(
                "ck_review_gate_configuration_deadline_order",
                "initial_submission_deadline_utc < faculty_response_deadline_utc AND faculty_response_deadline_utc < final_closure_deadline_utc");
        });

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.BatchId, x.GateType })
            .HasDatabaseName("ix_review_gate_configuration_batch_gate_type")
            .IsUnique();
        builder.HasIndex(x => x.CreatedBy)
            .HasDatabaseName("ix_review_gate_configuration_created_by");
        builder.HasIndex(x => x.UpdatedBy)
            .HasDatabaseName("ix_review_gate_configuration_updated_by");

        builder.Property(x => x.InitialSubmissionDeadlineUtc).IsRequired();
        builder.Property(x => x.FacultyResponseDeadlineUtc).IsRequired();
        builder.Property(x => x.FinalClosureDeadlineUtc).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
