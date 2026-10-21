using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Configurations;

public sealed class ScoreWeightConfigConfiguration : IEntityTypeConfiguration<ScoreWeightConfig>
{
    public void Configure(EntityTypeBuilder<ScoreWeightConfig> builder)
    {
        builder.ToTable("score_weight_config", "academic");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Navigation(x => x.Components).HasField("_components").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.BatchId, x.VersionNo })
            .HasDatabaseName("ix_score_weight_config_batch_version")
            .IsUnique();
        builder.HasIndex(x => x.BatchId)
            .HasDatabaseName("ix_score_weight_config_one_active")
            .HasFilter("status = 'Active'")
            .IsUnique();

        builder.Property(x => x.VersionNo).HasDefaultValue(1).HasSentinel(1);
        builder.Property(x => x.Status)
            .HasDefaultValue(ConfigurationVersionStatus.Draft)
            .HasSentinel(ConfigurationVersionStatus.Draft);
        builder.Property(x => x.CalculationMethod)
            .HasDefaultValue(ScoreCalculationMethod.IntegerCoefficient)
            .HasSentinel(ScoreCalculationMethod.IntegerCoefficient);
        builder.Property(x => x.ChangeReason).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasOne(x => x.Batch)
            .WithMany(x => x.ScoreConfigurations)
            .HasForeignKey(x => x.BatchId)
            .HasConstraintName("fk_score_weight_config_kltn_batch_batch_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
