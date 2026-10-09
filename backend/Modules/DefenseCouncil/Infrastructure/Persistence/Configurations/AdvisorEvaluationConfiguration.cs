using UGTM.Modules.DefenseCouncil.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class AdvisorEvaluationConfiguration : IEntityTypeConfiguration<AdvisorEvaluation>
{
    public void Configure(EntityTypeBuilder<AdvisorEvaluation> builder)
    {
        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_advisor_evaluation_group_id")
            .IsUnique();

        builder.ToTable("advisor_evaluation", "councils", table =>
            table.HasCheckConstraint("ck_advisor_evaluation_score", "score BETWEEN 0 AND 10"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsIgnoredInCalculation).HasDefaultValue(false);
        builder.Property(x => x.ExclusionReason).HasColumnType("text");

        builder.Property(x => x.Score)
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasColumnType("text");

        builder.Property(x => x.EvaluatedAt)
            .IsRequired();

    }
}
