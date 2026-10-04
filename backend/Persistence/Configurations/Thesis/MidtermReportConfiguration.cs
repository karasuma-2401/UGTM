using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Thesis;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Configurations.Thesis;

public class MidtermReportConfiguration : IEntityTypeConfiguration<MidtermReport>
{
    public void Configure(EntityTypeBuilder<MidtermReport> builder)
    {
        builder.ToTable("midterm_report", Schemas.Thesis);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.GroupId)
            .IsUnique();

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
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Reviewer)
            .WithMany()
            .HasForeignKey(x => x.ReviewedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
