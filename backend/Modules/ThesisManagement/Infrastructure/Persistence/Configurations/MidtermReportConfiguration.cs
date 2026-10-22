using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class MidtermReportConfiguration : IEntityTypeConfiguration<MidtermReport>
{
    public void Configure(EntityTypeBuilder<MidtermReport> builder)
    {
        builder.HasIndex(x => x.ReviewedBy)
            .HasDatabaseName("ix_midterm_report_reviewed_by");

        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_midterm_report_group_id")
            .IsUnique();

        builder.ToTable("midterm_report", "thesis");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CurrentVersionNo)
            .HasDefaultValue(1)
            .HasSentinel(1);

        builder.Property(x => x.Status)
            .HasDefaultValue(MidtermReportStatus.Submitted)
            .HasSentinel(MidtermReportStatus.Submitted);

        builder.Property(x => x.FacultyFeedback)
            .HasColumnType("text");

        builder.HasOne(x => x.Group)
            .WithOne()
            .HasForeignKey<MidtermReport>(x => x.GroupId)
            .HasConstraintName("fk_midterm_report_student_groups_group_id")
            .OnDelete(DeleteBehavior.Cascade);

    }
}
