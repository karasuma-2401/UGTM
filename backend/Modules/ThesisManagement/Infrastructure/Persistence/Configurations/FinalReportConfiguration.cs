using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class FinalReportConfiguration : IEntityTypeConfiguration<FinalReport>
{
    public void Configure(EntityTypeBuilder<FinalReport> builder)
    {
        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_final_report_group_id");

        builder.ToTable("final_report", "thesis");

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
            .HasConstraintName("fk_final_report_student_groups_group_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
