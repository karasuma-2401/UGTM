using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Academic;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Configurations.Academic;

public class KltnBatchConfiguration : IEntityTypeConfiguration<KltnBatch>
{
    public void Configure(EntityTypeBuilder<KltnBatch> builder)
    {
        builder.ToTable("kltn_batch", Schemas.Academic);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BatchName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.AcademicYear)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Semester)
            .IsRequired();

        builder.Property(x => x.StartDate)
            .IsRequired();

        builder.Property(x => x.EndDate)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasDefaultValue(BatchStatus.Draft)
            .HasSentinel(BatchStatus.Draft);

        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}
