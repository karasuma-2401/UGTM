using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Academic;

namespace UGTM.Persistence.Configurations.Academic;

public class ScoreWeightConfigConfiguration : IEntityTypeConfiguration<ScoreWeightConfig>
{
    public void Configure(EntityTypeBuilder<ScoreWeightConfig> builder)
    {
        builder.ToTable("score_weight_config", Schemas.Academic);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.BatchId)
            .IsUnique();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .HasSentinel(true);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Batch)
            .WithOne(b => b.ScoreWeightConfig)
            .HasForeignKey<ScoreWeightConfig>(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
