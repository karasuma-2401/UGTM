using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Scheduling;

namespace UGTM.Modules.AcademicPeriods.Tests;

internal static class AcademicPeriodTestData
{
    internal static readonly Guid ActorId = Guid.Parse("f0ea342b-d69e-40d6-9332-dfca175cff53");
    internal static readonly DateTime OccurredAtUtc = new(2026, 10, 21, 1, 0, 0, DateTimeKind.Utc);
    internal static readonly DateOnly BatchStart = new(2026, 9, 7);
    internal static readonly DateOnly BatchEnd = BatchStart.AddDays(146);

    internal static StageTemplateInput[] DefaultTemplateDefinitions()
        =>
        [
            Definition(1, "TOPIC_PREPARATION", 0, 6),
            Definition(2, "REGISTRATION_AND_SYLLABUS", 7, 13),
            Definition(3, "PROGRESS_EXECUTION", 14, 62),
            Definition(4, "MIDTERM_GATE", 63, 69),
            Definition(5, "FINAL_EVALUATION", 70, 125),
            Definition(6, "DEFENSE", 126, 139),
            Definition(7, "ARCHIVAL_AND_PUBLICATION", 140, 146)
        ];

    internal static ScheduleTemplateVersion ActiveTemplate()
    {
        var template = ScheduleTemplateVersion.CreateDraft(
            Guid.NewGuid(),
            "UIT_KLTN_STANDARD",
            1,
            "Standard schedule",
            KltnBatch.DefaultTimeZoneId,
            DefaultTemplateDefinitions(),
            ActorId,
            OccurredAtUtc);
        template.Publish(ActorId, OccurredAtUtc);
        return template;
    }

    internal static KltnBatch DraftBatch()
        => KltnBatch.Create(
            Guid.NewGuid(),
            "KLTN HK1 2026-2027",
            "2026-2027",
            1,
            BatchStart,
            BatchEnd,
            ActiveTemplate(),
            ActorId,
            OccurredAtUtc);

    private static StageTemplateInput Definition(int order, string code, int startOffset, int endOffset)
        => new(order, code, code, startOffset, endOffset, new TimeOnly(23, 59));
}
