using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class FinalReportConfiguration : IEntityTypeConfiguration<FinalReport>
{
    public void Configure(EntityTypeBuilder<FinalReport> builder)
    {
        builder.HasIndex(x => new { x.GroupId, x.VersionType })
            .HasDatabaseName("ux_final_report_group_id_version_type")
            .IsUnique();

        builder.ToTable("final_report", "thesis", table =>
            table.HasCheckConstraint("ck_final_report_current_version_no", "current_version_no > 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.VersionType)
            .IsRequired();

        builder.Property(x => x.CurrentVersionNo)
            .HasDefaultValue(1)
            .HasSentinel(1);

        builder.Property(x => x.Status)
            .HasDefaultValue(FinalReportStatus.Submitted)
            .HasSentinel(FinalReportStatus.Submitted);

        builder.Property(x => x.SubmittedAt)
            .IsRequired();

        builder.Property(x => x.Version)
            .IsRowVersion();

        builder.HasOne(x => x.Group)
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .HasConstraintName("fk_final_report_student_groups_group_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
