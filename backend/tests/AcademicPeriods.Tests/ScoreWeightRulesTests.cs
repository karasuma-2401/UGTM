using UGTM.Modules.AcademicPeriods.Domain.Grading;

namespace UGTM.Modules.AcademicPeriods.Tests;

public class ScoreWeightRulesTests
{
    [Fact]
    public void Percentage_configuration_requires_exactly_one_hundred()
    {
        ScoreWeightRules.ValidatePercentage([40m, 40m, 20m]);
        Assert.Throws<ArgumentException>(() => ScoreWeightRules.ValidatePercentage([40m, 40m, 10m]));
    }

    [Fact]
    public void Percentage_configuration_supports_fractional_weights()
    {
        ScoreWeightRules.ValidatePercentage([33.33m, 33.33m, 33.34m]);
    }

    [Fact]
    public void Coefficient_configuration_supports_both_confirmed_scenarios()
    {
        ScoreWeightRules.ValidateCoefficients([2m, 2m, 1m, 1m]);
        ScoreWeightRules.ValidateCoefficients([2m, 2m, 1m, 1m, 1m]);
    }

    [Fact]
    public void Coefficients_cannot_be_fractional_or_have_zero_total()
    {
        Assert.Throws<ArgumentException>(() => ScoreWeightRules.ValidateCoefficients([1.5m, 2m]));
        Assert.Throws<ArgumentException>(() => ScoreWeightRules.ValidateCoefficients([0m, 0m]));
    }

    [Fact]
    public void Negative_weights_are_rejected_in_both_modes()
    {
        Assert.Throws<ArgumentException>(() => ScoreWeightRules.ValidatePercentage([-10m, 110m]));
        Assert.Throws<ArgumentException>(() => ScoreWeightRules.ValidateCoefficients([-1m, 2m]));
    }
}
