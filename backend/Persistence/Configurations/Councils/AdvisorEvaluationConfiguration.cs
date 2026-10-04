using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Councils;

namespace UGTM.Persistence.Configurations.Councils;

public class AdvisorEvaluationConfiguration : IEntityTypeConfiguration<AdvisorEvaluation>
{
    public void Configure(EntityTypeBuilder<AdvisorEvaluation> builder)
    {
        builder.ToTable("advisor_evaluation", Schemas.Councils);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.GroupId)
            .IsUnique();

        builder.Property(x => x.Score)
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasColumnType("text");

        builder.Property(x => x.EvaluatedAt)
            .IsRequired();

        builder.HasOne(x => x.Group)
            .WithOne()
            .HasForeignKey<AdvisorEvaluation>(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
