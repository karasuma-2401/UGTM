using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Configurations;

public sealed class StageTemplateDefinitionConfiguration : IEntityTypeConfiguration<StageTemplateDefinition>
{
    public void Configure(EntityTypeBuilder<StageTemplateDefinition> builder)
    {
        builder.ToTable("stage_template_definition", "academic", table =>
        {
            table.HasCheckConstraint("ck_stage_template_order", "order_index BETWEEN 1 AND 7");
            table.HasCheckConstraint("ck_stage_template_offsets", "start_offset_days >= 0 AND end_offset_days >= start_offset_days");
        });

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TemplateVersionId, x.OrderIndex })
            .HasDatabaseName("ix_stage_template_version_order")
            .IsUnique();
        builder.HasIndex(x => new { x.TemplateVersionId, x.StageCode })
            .HasDatabaseName("ix_stage_template_version_code")
            .IsUnique();

        builder.Property(x => x.StageCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StageName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.DefaultDeadlineLocalTime).IsRequired();
        builder.Property(x => x.DeadlineAdjustmentPolicy).IsRequired();

        builder.HasOne(x => x.TemplateVersion)
            .WithMany(x => x.StageDefinitions)
            .HasForeignKey(x => x.TemplateVersionId)
            .HasConstraintName("fk_stage_template_definition_version")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(DefaultDefinitions());
    }

    private static object[] DefaultDefinitions()
    {
        var templateId = ScheduleTemplateVersionConfiguration.DefaultTemplateId;
        var deadline = new TimeOnly(23, 59);
        return
        [
            Definition("2f7fd572-7fc5-4620-972b-36cfcf8c0091", templateId, 1, "TOPIC_PREPARATION", "Chuẩn bị và công bố đề tài", 0, 6, deadline),
            Definition("11e9e9ab-789e-462f-8eb1-f2d6be05e3b3", templateId, 2, "REGISTRATION_AND_SYLLABUS", "Đăng ký và đề cương", 7, 13, deadline),
            Definition("abf58e41-725e-421b-b9f4-d6dcfe4dddc2", templateId, 3, "PROGRESS_EXECUTION", "Thực hiện và theo dõi tiến độ", 14, 62, deadline),
            Definition("a08f9813-286c-4da4-999b-4334a4cf3a20", templateId, 4, "MIDTERM_GATE", "Đánh giá giữa kỳ", 63, 69, deadline),
            Definition("e938156b-26e7-4256-9465-ac2a458e8c15", templateId, 5, "FINAL_EVALUATION", "Báo cáo cuối kỳ và phản biện", 70, 125, deadline),
            Definition("32488d73-1973-4ea6-b6d2-23fc1fd13a99", templateId, 6, "DEFENSE", "Hội đồng và bảo vệ", 126, 139, deadline),
            Definition("b2b70276-7538-451b-86a5-cf61031cfbd1", templateId, 7, "ARCHIVAL_AND_PUBLICATION", "Lưu chiểu và công bố kết quả", 140, 146, deadline)
        ];
    }

    private static object Definition(
        string id,
        Guid templateVersionId,
        int orderIndex,
        string stageCode,
        string stageName,
        int startOffsetDays,
        int endOffsetDays,
        TimeOnly deadline)
        => new
        {
            Id = Guid.Parse(id),
            TemplateVersionId = templateVersionId,
            StageCode = stageCode,
            StageName = stageName,
            OrderIndex = orderIndex,
            StartOffsetDays = startOffsetDays,
            EndOffsetDays = endOffsetDays,
            DefaultDeadlineLocalTime = deadline,
            DeadlineAdjustmentPolicy = DeadlineAdjustmentPolicy.None
        };
}
