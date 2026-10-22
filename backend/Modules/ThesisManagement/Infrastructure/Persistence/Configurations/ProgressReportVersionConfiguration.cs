using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public sealed class ProgressReportVersionConfiguration : IEntityTypeConfiguration<ProgressReportVersion>
{
    public void Configure(EntityTypeBuilder<ProgressReportVersion> builder)
    {
        builder.ToTable("progress_report_version", "thesis");
        builder.HasKey(version => version.Id);

        builder.HasIndex(version => new { version.ProgressReportId, version.VersionNo })
            .HasDatabaseName("ux_progress_report_version_report_id_version_no")
            .IsUnique();
        builder.HasIndex(version => version.SubmittedBy)
            .HasDatabaseName("ix_progress_report_version_submitted_by");
        builder.HasIndex(version => version.ReviewedBy)
            .HasDatabaseName("ix_progress_report_version_reviewed_by");

        builder.Property(version => version.VersionNo).IsRequired();
        builder.Property(version => version.GitUrl).HasMaxLength(2048).IsRequired();
        builder.Property(version => version.StudentNote).HasColumnType("text");
        builder.Property(version => version.SubmittedAt).IsRequired();
        builder.Property(version => version.Status).IsRequired();
        builder.Property(version => version.AdvisorFeedback).HasColumnType("text");

        builder.HasOne(version => version.ProgressReport)
            .WithMany()
            .HasForeignKey(version => version.ProgressReportId)
            .HasConstraintName("fk_progress_report_version_progress_report_report_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
