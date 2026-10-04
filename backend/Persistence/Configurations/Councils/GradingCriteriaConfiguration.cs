using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Councils;

namespace UGTM.Persistence.Configurations.Councils;

public class GradingCriteriaConfiguration : IEntityTypeConfiguration<GradingCriteria>
{
    public void Configure(EntityTypeBuilder<GradingCriteria> builder)
    {
        builder.ToTable("grading_criteria", Schemas.Councils);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CriteriaCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CriteriaName)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.MaxScore)
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.OrderIndex)
            .HasDefaultValue(1)
            .HasSentinel(1);

        builder.HasIndex(x => new { x.BatchId, x.CriteriaCode })
            .IsUnique();

        builder.HasOne(x => x.Batch)
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
