using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;

namespace UGTM.Modules.AcademicPeriods.Tests;

public sealed class ScoreConfigurationVersionTests
{
    [Fact]
    public void Active_configuration_is_immutable_and_can_be_cloned_as_next_version()
    {
        var configuration = ScoreWeightConfig.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            ScoreCalculationMethod.Percentage,
            PercentageComponents(),
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc);

        configuration.Publish(AcademicPeriodTestData.ActorId, AcademicPeriodTestData.OccurredAtUtc.AddMinutes(1));
        Assert.Equal(ConfigurationVersionStatus.Active, configuration.Status);
        Assert.Throws<InvalidOperationException>(() => configuration.UpdateDraft(
            ScoreCalculationMethod.Percentage,
            PercentageComponents(),
            AcademicPeriodTestData.OccurredAtUtc.AddMinutes(2),
            null));

        var next = configuration.CloneAsNextDraft(
            Guid.NewGuid(),
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc.AddMinutes(3),
            "Adjust weights");

        Assert.Equal(2, next.VersionNo);
        Assert.Equal(ConfigurationVersionStatus.Draft, next.Status);
        Assert.Equal(configuration.Components.Count, next.Components.Count);
        Assert.NotEqual(configuration.Components.First().Id, next.Components.First().Id);
    }

    [Fact]
    public void Configuration_rejects_duplicate_codes_and_invalid_minimum_score()
    {
        var duplicateCodes = PercentageComponents();
        duplicateCodes[1] = duplicateCodes[1] with { ComponentCode = duplicateCodes[0].ComponentCode };
        Assert.Throws<ArgumentException>(() => Create(duplicateCodes));

        var invalidMinimum = PercentageComponents();
        invalidMinimum[0] = invalidMinimum[0] with { MinimumScore = 10.01m };
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(invalidMinimum));
    }

    [Fact]
    public void Configuration_applies_selected_calculation_method()
    {
        var fractionalCoefficient = PercentageComponents();
        fractionalCoefficient[0] = fractionalCoefficient[0] with { Weight = 1.5m };
        fractionalCoefficient[1] = fractionalCoefficient[1] with { Weight = 2m };

        Assert.Throws<ArgumentException>(() => ScoreWeightConfig.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            ScoreCalculationMethod.IntegerCoefficient,
            fractionalCoefficient,
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc));
    }

    private static ScoreWeightConfig Create(IEnumerable<ScoreComponentDefinition> components)
        => ScoreWeightConfig.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            ScoreCalculationMethod.Percentage,
            components,
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc);

    private static ScoreComponentDefinition[] PercentageComponents()
        =>
        [
            new("ADVISOR", "Advisor", ScoreComponentSource.Advisor, ScoreComponentApplicability.Always, 40m, true, null, false, 1),
            new("REVIEWER", "Reviewer", ScoreComponentSource.Reviewer, ScoreComponentApplicability.Always, 30m, true, 5m, true, 2),
            new("COUNCIL", "Council", ScoreComponentSource.CouncilAverage, ScoreComponentApplicability.Always, 30m, true, null, false, 3)
        ];
}
