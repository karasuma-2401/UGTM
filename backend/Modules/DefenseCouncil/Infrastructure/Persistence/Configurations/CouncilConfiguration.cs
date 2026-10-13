using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class CouncilConfiguration : IEntityTypeConfiguration<Council>
{
    public void Configure(EntityTypeBuilder<Council> builder)
    {
        builder.HasIndex(x => x.BatchId)
            .HasDatabaseName("ix_council_batch_id");

        builder.HasIndex(x => x.ApprovedBy)
            .HasDatabaseName("ix_council_approved_by");

        builder.ToTable("council", "councils");

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

    }
}
