using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Councils;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Configurations.Councils;

public class CouncilConfiguration : IEntityTypeConfiguration<Council>
{
    public void Configure(EntityTypeBuilder<Council> builder)
    {
        builder.ToTable("council", Schemas.Councils);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CouncilCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DefenseDate)
            .IsRequired();

        builder.Property(x => x.TimeSlot)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Room)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasDefaultValue(CouncilStatus.Draft)
            .HasSentinel(CouncilStatus.Draft);

        builder.Property(x => x.DecisionNumber)
            .HasMaxLength(100);

        builder.Property(x => x.DecisionFileUrl)
            .HasMaxLength(500);

        builder.HasOne(x => x.Batch)
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
