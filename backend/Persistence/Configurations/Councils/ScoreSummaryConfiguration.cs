using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Councils;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Configurations.Councils;

public class ScoreSummaryConfiguration : IEntityTypeConfiguration<ScoreSummary>
{
    public void Configure(EntityTypeBuilder<ScoreSummary> builder)
    {
        builder.ToTable("score_summary", Schemas.Councils);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.GroupId)
            .IsUnique();

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

        builder.HasOne(x => x.Group)
            .WithOne()
            .HasForeignKey<ScoreSummary>(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PublishedByUser)
            .WithMany()
            .HasForeignKey(x => x.PublishedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
