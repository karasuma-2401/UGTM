using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Thesis;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Configurations.Thesis;

public class FinalReportConfiguration : IEntityTypeConfiguration<FinalReport>
{
    public void Configure(EntityTypeBuilder<FinalReport> builder)
    {
        builder.ToTable("final_report", Schemas.Thesis);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.VersionType)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasDefaultValue(FinalReportStatus.Submitted)
            .HasSentinel(FinalReportStatus.Submitted);

        builder.Property(x => x.SubmittedAt)
            .IsRequired();

        builder.HasOne(x => x.Group)
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
