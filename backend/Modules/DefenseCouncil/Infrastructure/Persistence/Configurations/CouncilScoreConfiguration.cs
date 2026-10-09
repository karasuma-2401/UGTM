using UGTM.Modules.DefenseCouncil.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class CouncilScoreConfiguration : IEntityTypeConfiguration<CouncilScore>
{
    public void Configure(EntityTypeBuilder<CouncilScore> builder)
    {
        builder.HasIndex(x => new { x.CouncilAssignmentId, x.CouncilMemberId })
            .HasDatabaseName("ix_council_score_council_assignment_id_council_member_id")
            .IsUnique();

        builder.HasIndex(x => x.CouncilMemberId)
            .HasDatabaseName("ix_council_score_council_member_id");

        builder.ToTable("council_score", "councils", table =>
            table.HasCheckConstraint("ck_council_score_score", "score BETWEEN 0 AND 10"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsIgnoredInCalculation).HasDefaultValue(false);
        builder.Property(x => x.ExclusionReason).HasColumnType("text");

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
            .HasConstraintName("fk_council_score_council_assignment_council_assignment_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CouncilMember)
            .WithMany(m => m.Scores)
            .HasForeignKey(x => x.CouncilMemberId)
            .HasConstraintName("fk_council_score_council_member_council_member_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
