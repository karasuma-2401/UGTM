using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Councils;

namespace UGTM.Persistence.Configurations.Councils;

public class EvaluationDetailConfiguration : IEntityTypeConfiguration<EvaluationDetail>
{
    public void Configure(EntityTypeBuilder<EvaluationDetail> builder)
    {
        builder.ToTable("evaluation_detail", Schemas.Councils);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.CriteriaId, x.EvaluationId })
            .IsUnique();

        builder.Property(x => x.Score)
            .HasPrecision(4, 2)
            .IsRequired();

        builder.HasOne(x => x.Criteria)
            .WithMany()
            .HasForeignKey(x => x.CriteriaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
