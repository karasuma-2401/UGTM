using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Thesis;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Configurations.Thesis;

public class ProgressReportConfiguration : IEntityTypeConfiguration<ProgressReport>
{
    public void Configure(EntityTypeBuilder<ProgressReport> builder)
    {
        builder.ToTable("progress_report", Schemas.Thesis);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.MilestoneId, x.GroupId })
            .IsUnique();

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
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Group)
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
