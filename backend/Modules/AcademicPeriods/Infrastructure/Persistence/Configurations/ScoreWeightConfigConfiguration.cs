using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Configurations;

public class ScoreWeightConfigConfiguration : IEntityTypeConfiguration<ScoreWeightConfig>
{
    public void Configure(EntityTypeBuilder<ScoreWeightConfig> builder)
    {
        builder.HasIndex(x => x.BatchId)
            .HasDatabaseName("ix_score_weight_config_batch_id")
            .IsUnique();

        builder.ToTable("score_weight_config", "academic");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.VersionNo).HasDefaultValue(1).HasSentinel(1);
        builder.Property(x => x.CalculationMethod)
            .HasDefaultValue(ScoreCalculationMethod.IntegerCoefficient)
            .HasSentinel(ScoreCalculationMethod.IntegerCoefficient);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .HasSentinel(true);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Batch)
            .WithOne(b => b.ScoreWeightConfig)
            .HasForeignKey<ScoreWeightConfig>(x => x.BatchId)
            .HasConstraintName("fk_score_weight_config_kltn_batch_batch_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
