using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class FinalSubmissionConfiguration : IEntityTypeConfiguration<FinalSubmission>
{
    public void Configure(EntityTypeBuilder<FinalSubmission> builder)
    {
        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_final_submission_group_id")
            .IsUnique();

        builder.HasIndex(x => x.ArchivedConfirmedBy)
            .HasDatabaseName("ix_final_submission_archived_confirmed_by");

        builder.HasIndex(x => x.AdvisorConfirmedBy)
            .HasDatabaseName("ix_final_submission_advisor_confirmed_by");

        builder.ToTable("final_submission", "thesis");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.CurrentVersionNo).HasDefaultValue(1).HasSentinel(1);
        builder.Property(x => x.AbstractVi).HasColumnType("text");
        builder.Property(x => x.AbstractEn).HasColumnType("text");
        builder.Property(x => x.KeywordsVi).HasMaxLength(1000);
        builder.Property(x => x.KeywordsEn).HasMaxLength(1000);
        builder.Property(x => x.FacultyFeedback).HasColumnType("text");

        builder.Property(x => x.Status)
            .HasDefaultValue(FinalSubmissionStatus.NotSubmitted)
            .HasSentinel(FinalSubmissionStatus.NotSubmitted);

        builder.HasOne(x => x.Group)
            .WithOne()
            .HasForeignKey<FinalSubmission>(x => x.GroupId)
            .HasConstraintName("fk_final_submission_student_groups_group_id")
            .OnDelete(DeleteBehavior.Cascade);

    }
}
