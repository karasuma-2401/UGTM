using UGTM.Modules.DefenseCouncil.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class CouncilMinuteConfiguration : IEntityTypeConfiguration<CouncilMinute>
{
    public void Configure(EntityTypeBuilder<CouncilMinute> builder)
    {
        builder.HasIndex(x => x.UnlockedBy)
            .HasDatabaseName("ix_council_minute_unlocked_by");

        builder.HasIndex(x => x.LockedBy)
            .HasDatabaseName("ix_council_minute_locked_by");

        builder.HasIndex(x => x.CouncilAssignmentId)
            .HasDatabaseName("ix_council_minute_council_assignment_id")
            .IsUnique();

        builder.ToTable("council_minute", "councils");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GeneralFeedback)
            .HasColumnType("text");

        builder.Property(x => x.RequiredRevisions)
            .HasColumnType("text");

        builder.Property(x => x.LockStatus)
            .HasDefaultValue(CouncilMinuteLockStatus.Open)
            .HasSentinel(CouncilMinuteLockStatus.Open);

        builder.Property(x => x.OverrideReason)
            .HasColumnType("text");

        builder.HasOne(x => x.Assignment)
            .WithOne(a => a.Minute)
            .HasForeignKey<CouncilMinute>(x => x.CouncilAssignmentId)
            .HasConstraintName("fk_council_minute_council_assignment_council_assignment_id")
            .OnDelete(DeleteBehavior.Cascade);

    }
}
