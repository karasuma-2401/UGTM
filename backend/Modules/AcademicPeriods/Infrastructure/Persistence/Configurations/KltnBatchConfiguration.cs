using UGTM.Modules.AcademicPeriods.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Configurations;

public class KltnBatchConfiguration : IEntityTypeConfiguration<KltnBatch>
{
    public void Configure(EntityTypeBuilder<KltnBatch> builder)
    {
        builder.ToTable("kltn_batch", "academic", table =>
        {
            table.HasCheckConstraint("ck_batch_dates", "end_date > start_date");
            table.HasCheckConstraint("ck_batch_semester", "semester > 0");
        });

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
