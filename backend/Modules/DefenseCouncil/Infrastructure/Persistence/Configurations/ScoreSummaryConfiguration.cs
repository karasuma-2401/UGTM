using UGTM.Modules.DefenseCouncil.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class ScoreSummaryConfiguration : IEntityTypeConfiguration<ScoreSummary>
{
    public void Configure(EntityTypeBuilder<ScoreSummary> builder)
    {
        builder.HasIndex(x => x.PublishedBy)
            .HasDatabaseName("ix_score_summary_published_by");

        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_score_summary_group_id")
            .IsUnique();

        builder.ToTable("score_summary", "councils");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.TotalWeight).HasPrecision(12, 4);
        builder.Property(x => x.CalculationConfigSnapshot).HasColumnType("jsonb");

        builder.Property(x => x.AdvisorScore)
            .HasPrecision(4, 2);

        builder.Property(x => x.ReviewerScore)
            .HasPrecision(4, 2);

        builder.Property(x => x.CouncilScore)
            .HasPrecision(4, 2);

        builder.Property(x => x.FinalScore)
            .HasPrecision(4, 2);

        builder.Property(x => x.CalculationFormula)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasDefaultValue(ScoreSummaryStatus.Pending)
            .HasSentinel(ScoreSummaryStatus.Pending);

    }
}
