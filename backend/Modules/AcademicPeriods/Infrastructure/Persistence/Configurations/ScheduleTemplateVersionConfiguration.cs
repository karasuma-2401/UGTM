using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Configurations;

public sealed class ScheduleTemplateVersionConfiguration : IEntityTypeConfiguration<ScheduleTemplateVersion>
{
    public static readonly Guid DefaultTemplateId = Guid.Parse("4e035a70-f7d5-49ef-b977-d818eff26896");
    private static readonly DateTime SeededAtUtc = new(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<ScheduleTemplateVersion> builder)
    {
        builder.ToTable("schedule_template_version", "academic");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Navigation(x => x.StageDefinitions).HasField("_stageDefinitions").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.TemplateCode, x.VersionNo })
            .HasDatabaseName("ix_schedule_template_code_version")
            .IsUnique();
        builder.HasIndex(x => x.TemplateCode)
            .HasDatabaseName("ix_schedule_template_one_active")
            .HasFilter("status = 'Active'")
            .IsUnique();

        builder.Property(x => x.TemplateCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.VersionNo).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasData(new
        {
            Id = DefaultTemplateId,
            TemplateCode = "UIT_KLTN_STANDARD",
            VersionNo = 1,
            Name = "Quy trình KLTN chuẩn 21 tuần",
            TimeZoneId = KltnBatch.DefaultTimeZoneId,
            Status = ConfigurationVersionStatus.Active,
            CreatedBy = (Guid?)null,
            CreatedAt = SeededAtUtc,
            PublishedBy = (Guid?)null,
            PublishedAt = (DateTime?)SeededAtUtc,
            RetiredAt = (DateTime?)null
        });
    }
}
