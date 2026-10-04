using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Councils;

namespace UGTM.Persistence.Configurations.Councils;

public class CouncilScoreConfiguration : IEntityTypeConfiguration<CouncilScore>
{
    public void Configure(EntityTypeBuilder<CouncilScore> builder)
    {
        builder.ToTable("council_score", Schemas.Councils);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.CouncilAssignmentId, x.CouncilMemberId })
            .IsUnique();

        builder.Property(x => x.Score)
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.Feedback)
            .HasColumnType("text");

        builder.Property(x => x.EvaluatedAt)
            .IsRequired();

        builder.HasOne(x => x.Assignment)
            .WithMany(a => a.Scores)
            .HasForeignKey(x => x.CouncilAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CouncilMember)
            .WithMany(m => m.Scores)
            .HasForeignKey(x => x.CouncilMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
