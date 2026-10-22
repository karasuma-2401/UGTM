using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Domain.Grading;

public static class ScoreWeightRules
{
    public static void Validate(ScoreCalculationMethod method, IEnumerable<decimal> weights)
    {
        switch (method)
        {
            case ScoreCalculationMethod.Percentage:
                ValidatePercentage(weights);
                break;
            case ScoreCalculationMethod.IntegerCoefficient:
                ValidateCoefficients(weights);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported score calculation method.");
        }
    }

    public static void ValidatePercentage(IEnumerable<decimal> weights)
    {
        var values = ReadNonnegativeWeights(weights);
        if (values.Sum() != 100m)
        {
            throw new ArgumentException("Percentage weights must sum to 100.", nameof(weights));
        }
    }

    public static void ValidateCoefficients(IEnumerable<decimal> weights)
    {
        var values = ReadNonnegativeWeights(weights);
        if (values.Any(x => decimal.Truncate(x) != x) || values.Sum() <= 0m)
        {
            throw new ArgumentException("Coefficients must be nonnegative integers with a positive total.", nameof(weights));
        }
    }

    private static decimal[] ReadNonnegativeWeights(IEnumerable<decimal> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var values = weights.ToArray();
        if (values.Length == 0 || values.Any(x => x < 0m))
        {
            throw new ArgumentException("At least one nonnegative weight is required.", nameof(weights));
        }

        return values;
    }
}
