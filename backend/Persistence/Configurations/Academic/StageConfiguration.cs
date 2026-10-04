using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Academic;

namespace UGTM.Persistence.Configurations.Academic;

public class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("stage", Schemas.Academic);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StageIndex)
            .IsRequired();

        builder.Property(x => x.StageName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.StartDate)
            .IsRequired();

        builder.Property(x => x.DeadlineDate)
            .IsRequired();

        builder.HasOne(x => x.Batch)
            .WithMany(b => b.Stages)
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
