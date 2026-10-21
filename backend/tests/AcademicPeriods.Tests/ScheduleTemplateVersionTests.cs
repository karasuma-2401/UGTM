using UGTM.Modules.AcademicPeriods.Domain.Entities;

namespace UGTM.Modules.AcademicPeriods.Tests;

public sealed class ScheduleTemplateVersionTests
{
    [Fact]
    public void Template_requires_exactly_seven_stages()
    {
        var definitions = AcademicPeriodTestData.DefaultTemplateDefinitions()[..6];

        Assert.Throws<ArgumentException>(() => ScheduleTemplateVersion.CreateDraft(
            Guid.NewGuid(),
            "INVALID",
            1,
            "Invalid template",
            KltnBatch.DefaultTimeZoneId,
            definitions,
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc));
    }

    [Fact]
    public void Template_rejects_overlapping_offsets()
    {
        var definitions = AcademicPeriodTestData.DefaultTemplateDefinitions();
        definitions[1] = definitions[1] with { StartOffsetDays = 6 };

        Assert.Throws<ArgumentException>(() => ScheduleTemplateVersion.CreateDraft(
            Guid.NewGuid(),
            "INVALID",
            1,
            "Invalid template",
            KltnBatch.DefaultTimeZoneId,
            definitions,
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc));
    }
}
