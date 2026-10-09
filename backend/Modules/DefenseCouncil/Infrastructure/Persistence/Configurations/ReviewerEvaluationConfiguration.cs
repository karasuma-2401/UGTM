using UGTM.Modules.DefenseCouncil.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class ReviewerEvaluationConfiguration : IEntityTypeConfiguration<ReviewerEvaluation>
{
    public void Configure(EntityTypeBuilder<ReviewerEvaluation> builder)
    {
        builder.HasIndex(x => x.AssignmentId)
            .HasDatabaseName("ix_reviewer_evaluation_assignment_id")
            .IsUnique();

        builder.ToTable("reviewer_evaluation", "councils", table =>
            table.HasCheckConstraint("ck_reviewer_evaluation_score", "score BETWEEN 0 AND 10"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsIgnoredInCalculation).HasDefaultValue(false);
        builder.Property(x => x.ExclusionReason).HasColumnType("text");

        builder.Property(x => x.Score)
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasColumnType("text");

        builder.Property(x => x.DefenseApproval)
            .IsRequired();

        builder.Property(x => x.IsVisibleToStudent)
            .HasDefaultValue(false)
            .HasSentinel(false);

        builder.Property(x => x.EvaluatedAt)
            .IsRequired();

        builder.HasOne(x => x.Assignment)
            .WithOne(a => a.Evaluation)
            .HasForeignKey<ReviewerEvaluation>(x => x.AssignmentId)
            .HasConstraintName("fk_reviewer_evaluation_reviewer_assignment_assignment_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
