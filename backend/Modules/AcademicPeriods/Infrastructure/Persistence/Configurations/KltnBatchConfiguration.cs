using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Configurations;

public sealed class KltnBatchConfiguration : IEntityTypeConfiguration<KltnBatch>
{
    public void Configure(EntityTypeBuilder<KltnBatch> builder)
    {
        builder.ToTable("kltn_batch", "academic", table =>
        {
            table.HasCheckConstraint("ck_batch_dates", "end_date > start_date");
            table.HasCheckConstraint("ck_batch_semester", "semester > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Navigation(x => x.Stages).HasField("_stages").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.ScoreConfigurations).HasField("_scoreConfigurations").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.AcademicYear, x.Semester })
            .HasDatabaseName("ix_kltn_batch_academic_year_semester")
            .IsUnique();

        builder.Property(x => x.BatchName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AcademicYear).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Semester).IsRequired();
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate).IsRequired();
        builder.Property(x => x.TimeZoneId)
            .HasMaxLength(100)
            .HasDefaultValue(KltnBatch.DefaultTimeZoneId)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasDefaultValue(BatchStatus.Draft)
            .HasSentinel(BatchStatus.Draft);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasOne(x => x.ScheduleTemplateVersion)
            .WithMany()
            .HasForeignKey(x => x.ScheduleTemplateVersionId)
            .HasConstraintName("fk_kltn_batch_schedule_template_version")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
