namespace UGTM.Modules.DefenseCouncil.Domain.Grading;

public sealed class ThesisGradeCalculator
{
    public GradeCalculationResult Calculate(IEnumerable<GradeContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        var items = contributions.ToArray();

        if (items.Length == 0 || items.Any(x => string.IsNullOrWhiteSpace(x.Code))
            || items.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != items.Length)
        {
            throw new ArgumentException("Grade contributions must have distinct, nonempty codes.", nameof(contributions));
        }

        if (items.Any(x => x.Score is < 0m or > 10m || x.Weight < 0m))
        {
            throw new ArgumentException("Scores must be between 0 and 10 and weights must be nonnegative.", nameof(contributions));
        }

        var accepted = items.Where(x => !x.IsIgnoredInCalculation).ToArray();
        var totalWeight = accepted.Sum(x => x.Weight);
        if (totalWeight <= 0m)
        {
            throw new InvalidOperationException("No positive weight remains for a valid final score.");
        }

        return new GradeCalculationResult(
            accepted.Sum(x => x.Score * x.Weight) / totalWeight,
            totalWeight,
            Array.AsReadOnly(items));
    }

    public GradeCalculationResult CalculateDefault(
        decimal advisorScore,
        decimal reviewerScore,
        decimal chairmanScore,
        decimal secretaryScore,
        bool reviewerIsCouncilMember,
        decimal? memberScore = null,
        IReadOnlySet<string>? ignoredCodes = null)
    {
        if (reviewerIsCouncilMember && memberScore.HasValue)
        {
            throw new ArgumentException("The reviewer serving as council member must not enter a second council score.", nameof(memberScore));
        }

        if (!reviewerIsCouncilMember && !memberScore.HasValue)
        {
            throw new ArgumentException("The council member score is required when the reviewer is outside the council.", nameof(memberScore));
        }

        List<GradeContribution> items =
        [
            new("ADVISOR", advisorScore, 2m),
            new("REVIEWER", reviewerScore, 2m),
            new("COUNCIL_CHAIRMAN", chairmanScore, 1m),
            new("COUNCIL_SECRETARY", secretaryScore, 1m),
        ];

        if (!reviewerIsCouncilMember)
        {
            items.Add(new("COUNCIL_MEMBER", memberScore!.Value, 1m));
        }

        if (ignoredCodes is not null)
        {
            if (ignoredCodes.Except(items.Select(x => x.Code), StringComparer.Ordinal).Any())
            {
                throw new ArgumentException("Only existing grade contributions can be excluded.", nameof(ignoredCodes));
            }

            items = items.Select(x => x with { IsIgnoredInCalculation = ignoredCodes.Contains(x.Code) }).ToList();
        }

        return Calculate(items);
    }
}
