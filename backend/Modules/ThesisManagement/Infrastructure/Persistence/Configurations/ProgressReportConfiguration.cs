using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class ProgressReportConfiguration : IEntityTypeConfiguration<ProgressReport>
{
    public void Configure(EntityTypeBuilder<ProgressReport> builder)
    {
        builder.HasIndex(x => new { x.MilestoneId, x.GroupId })
            .HasDatabaseName("ix_progress_report_milestone_id_group_id")
            .IsUnique();

        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_progress_report_group_id");

        builder.ToTable("progress_report", "thesis");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CurrentVersionNo)
            .HasDefaultValue(1)
            .HasSentinel(1);

        builder.Property(x => x.Status)
            .HasDefaultValue(ProgressReportStatus.Submitted)
            .HasSentinel(ProgressReportStatus.Submitted);

        builder.Property(x => x.StudentNote)
            .HasColumnType("text");

        builder.Property(x => x.SubmittedAt)
            .IsRequired();

        builder.Property(x => x.AdvisorFeedback)
            .HasColumnType("text");

        builder.HasOne(x => x.Milestone)
            .WithMany(m => m.ProgressReports)
            .HasForeignKey(x => x.MilestoneId)
            .HasConstraintName("fk_progress_report_progress_milestone_milestone_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Group)
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .HasConstraintName("fk_progress_report_student_groups_group_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
