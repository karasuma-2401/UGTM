using UGTM.Modules.DefenseCouncil.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class ScoreSummaryComponentConfiguration : IEntityTypeConfiguration<ScoreSummaryComponent>
{
    public void Configure(EntityTypeBuilder<ScoreSummaryComponent> builder)
    {
        builder.HasIndex(x => new { x.SummaryId, x.ComponentCode, x.SourceKey })
            .HasDatabaseName("ix_score_summary_component_summary_id_component_code_source_key")
            .IsUnique();

        builder.ToTable("score_summary_component", "councils", table =>
        {
            table.HasCheckConstraint("ck_summary_component_score", "score BETWEEN 0 AND 10");
            table.HasCheckConstraint("ck_summary_component_weight", "weight >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ComponentCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceKey).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Score).HasPrecision(6, 4);
        builder.Property(x => x.Weight).HasPrecision(9, 4);
        builder.HasOne(x => x.Summary).WithMany(x => x.Components)
            .HasForeignKey(x => x.SummaryId)
            .HasConstraintName("fk_score_summary_component_score_summaries_summary_id").OnDelete(DeleteBehavior.Cascade);
    }
}
