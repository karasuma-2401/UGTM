using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class FinalReportPlagiarismScanConfiguration : IEntityTypeConfiguration<FinalReportPlagiarismScan>
{
    public void Configure(EntityTypeBuilder<FinalReportPlagiarismScan> builder)
    {
        builder.ToTable("final_report_plagiarism_scan", "thesis", table =>
        {
            table.HasCheckConstraint(
                "ck_final_report_plagiarism_scan_version_no",
                "version_no > 0");
            table.HasCheckConstraint(
                "ck_final_report_plagiarism_scan_similarity_coverage",
                "similarity_coverage IS NULL OR (similarity_coverage >= 0 AND similarity_coverage <= 100)");
            table.HasCheckConstraint(
                "ck_final_report_plagiarism_scan_max_reranker_score",
                "max_reranker_score IS NULL OR (max_reranker_score >= 0 AND max_reranker_score <= 1)");
        });

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.FinalReportId, x.VersionNo })
            .HasDatabaseName("ux_final_report_plagiarism_scan_report_id_version_no")
            .IsUnique();

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_final_report_plagiarism_scan_status");

        builder.HasIndex(x => x.JobId)
            .HasDatabaseName("ix_final_report_plagiarism_scan_job_id");

        builder.HasIndex(x => x.WorkerPartition)
            .HasDatabaseName("ix_final_report_plagiarism_scan_worker_partition");

        builder.Property(x => x.FinalReportId)
            .IsRequired();

        builder.Property(x => x.SubmissionFileId)
            .IsRequired();

        builder.Property(x => x.VersionNo)
            .IsRequired();

        builder.Property(x => x.JobId)
            .IsRequired(false);

        builder.Property(x => x.Status)
            .HasDefaultValue(PlagiarismScanStatus.PendingSubmission)
            .HasSentinel(PlagiarismScanStatus.PendingSubmission)
            .IsRequired();

        builder.Property(x => x.SimilarityCoverage)
            .IsRequired(false);

        builder.Property(x => x.MaxRerankerScore)
            .IsRequired(false);

        builder.Property(x => x.ScannedChunks)
            .IsRequired(false);

        builder.Property(x => x.ScannedCharacters)
            .IsRequired(false);

        builder.Property(x => x.MatchedCharacters)
            .IsRequired(false);

        builder.Property(x => x.CitedFindingsCount)
            .IsRequired(false);

        builder.Property(x => x.UncitedFindingsCount)
            .IsRequired(false);

        builder.Property(x => x.SuspectedFindingsCount)
            .IsRequired(false);

        builder.Property(x => x.RawReportJson)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(x => x.AttemptCount)
            .HasDefaultValue(0)
            .HasSentinel(0)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastPolledAt)
            .IsRequired(false);

        builder.Property(x => x.CompletedAt)
            .IsRequired(false);

        builder.Property(x => x.NextCheckAfterUtc)
            .IsRequired(false);

        builder.Property(x => x.WorkerPartition)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(x => x.Version)
            .IsRowVersion();

        builder.HasOne(x => x.FinalReport)
            .WithMany()
            .HasForeignKey(x => x.FinalReportId)
            .HasConstraintName("fk_final_report_plagiarism_scan_final_report_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SubmissionFile)
            .WithMany()
            .HasForeignKey(x => x.SubmissionFileId)
            .HasConstraintName("fk_final_report_plagiarism_scan_submission_file_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

