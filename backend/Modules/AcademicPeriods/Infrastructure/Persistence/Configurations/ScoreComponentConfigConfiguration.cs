using UGTM.Modules.AcademicPeriods.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Configurations;

public class ScoreComponentConfigConfiguration : IEntityTypeConfiguration<ScoreComponentConfig>
{
    public void Configure(EntityTypeBuilder<ScoreComponentConfig> builder)
    {
        builder.HasIndex(x => new { x.ConfigId, x.ComponentCode })
            .HasDatabaseName("ix_score_component_config_config_id_component_code")
            .IsUnique();

        builder.ToTable("score_component_config", "academic", table =>
        {
            table.HasCheckConstraint("ck_score_component_weight", "weight >= 0");
            table.HasCheckConstraint("ck_score_component_minimum", "minimum_score IS NULL OR minimum_score BETWEEN 0 AND 10");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ComponentCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ComponentName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Weight).HasPrecision(9, 4);
        builder.Property(x => x.MinimumScore).HasPrecision(4, 2);
        builder.HasOne(x => x.Config).WithMany(x => x.Components)
            .HasForeignKey(x => x.ConfigId)
            .HasConstraintName("fk_score_component_config_score_weight_configs_config_id").OnDelete(DeleteBehavior.Cascade);
    }
}
