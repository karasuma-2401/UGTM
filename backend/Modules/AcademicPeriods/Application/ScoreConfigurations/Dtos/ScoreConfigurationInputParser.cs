using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Application.Common;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;

namespace UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;

internal static class ScoreConfigurationInputParser
{
    internal static Result<ParsedScoreConfiguration> Parse(
        string? calculationMethod,
        IReadOnlyCollection<ScoreComponentInput>? components)
    {
        if (string.IsNullOrWhiteSpace(calculationMethod)
            || int.TryParse(calculationMethod.Trim(), out _)
            || !Enum.TryParse<ScoreCalculationMethod>(calculationMethod, true, out var method)
            || !Enum.IsDefined(method))
        {
            return Result.Failure<ParsedScoreConfiguration>(ScoreConfigurationErrors.InvalidRequest);
        }

        if (components is null || components.Count == 0)
        {
            return Result.Failure<ParsedScoreConfiguration>(ScoreConfigurationErrors.EmptyComponents);
        }

        if (components.Any(component => string.IsNullOrWhiteSpace(component.ComponentCode)
                                        || string.IsNullOrWhiteSpace(component.ComponentName)))
        {
            return Result.Failure<ParsedScoreConfiguration>(ScoreConfigurationErrors.InvalidRequest);
        }

        if (components.Select(component => component.ComponentCode.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != components.Count)
        {
            return Result.Failure<ParsedScoreConfiguration>(ScoreConfigurationErrors.DuplicateComponentCode);
        }

        if (components.Any(component => component.OrderIndex <= 0)
            || components.Select(component => component.OrderIndex).Distinct().Count() != components.Count)
        {
            return Result.Failure<ParsedScoreConfiguration>(ScoreConfigurationErrors.DuplicateComponentOrder);
        }

        if (components.Any(component => component.MinimumScore is < 0 or > 10))
        {
            return Result.Failure<ParsedScoreConfiguration>(ScoreConfigurationErrors.InvalidMinimumScore);
        }

        if (method == ScoreCalculationMethod.Percentage
            && (components.Any(component => component.Weight < 0)
                || components.Sum(component => component.Weight) != 100m))
        {
            return Result.Failure<ParsedScoreConfiguration>(ScoreConfigurationErrors.InvalidPercentageTotal);
        }

        if (method == ScoreCalculationMethod.IntegerCoefficient
            && (components.Any(component => component.Weight < 0
                                            || component.Weight != decimal.Truncate(component.Weight))
                || components.Sum(component => component.Weight) <= 0m))
        {
            return Result.Failure<ParsedScoreConfiguration>(ScoreConfigurationErrors.InvalidCoefficient);
        }

        var definitions = new List<ScoreComponentDefinition>(components.Count);
        foreach (var component in components)
        {
            if (string.IsNullOrWhiteSpace(component.Source)
                || int.TryParse(component.Source.Trim(), out _)
                || !Enum.TryParse<ScoreComponentSource>(component.Source, true, out var source)
                || !Enum.IsDefined(source)
                || string.IsNullOrWhiteSpace(component.Applicability)
                || int.TryParse(component.Applicability.Trim(), out _)
                || !Enum.TryParse<ScoreComponentApplicability>(component.Applicability, true, out var applicability)
                || !Enum.IsDefined(applicability))
            {
                return Result.Failure<ParsedScoreConfiguration>(ScoreConfigurationErrors.InvalidRequest);
            }

            definitions.Add(new ScoreComponentDefinition(
                component.ComponentCode,
                component.ComponentName,
                source,
                applicability,
                component.Weight,
                component.IsRequired,
                component.MinimumScore,
                component.RequiresApproval,
                component.OrderIndex));
        }

        return Result.Success(new ParsedScoreConfiguration(method, definitions));
    }
}

internal sealed record ParsedScoreConfiguration(
    ScoreCalculationMethod CalculationMethod,
    IReadOnlyCollection<ScoreComponentDefinition> Components);
