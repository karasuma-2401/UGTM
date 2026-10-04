using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Councils;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Configurations.Councils;

public class CouncilMinuteConfiguration : IEntityTypeConfiguration<CouncilMinute>
{
    public void Configure(EntityTypeBuilder<CouncilMinute> builder)
    {
        builder.ToTable("council_minute", Schemas.Councils);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.CouncilAssignmentId)
            .IsUnique();

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
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LockedByUser)
            .WithMany()
            .HasForeignKey(x => x.LockedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
