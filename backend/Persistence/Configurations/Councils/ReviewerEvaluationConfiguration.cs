using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Councils;

namespace UGTM.Persistence.Configurations.Councils;

public class ReviewerEvaluationConfiguration : IEntityTypeConfiguration<ReviewerEvaluation>
{
    public void Configure(EntityTypeBuilder<ReviewerEvaluation> builder)
    {
        builder.ToTable("reviewer_evaluation", Schemas.Councils);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.AssignmentId)
            .IsUnique();

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
            .OnDelete(DeleteBehavior.Cascade);
    }
}
