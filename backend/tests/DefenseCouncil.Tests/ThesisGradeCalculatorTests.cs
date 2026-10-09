using UGTM.Modules.DefenseCouncil.Domain.Grading;

namespace UGTM.Modules.DefenseCouncil.Tests;

public class ThesisGradeCalculatorTests
{
    private readonly ThesisGradeCalculator _calculator = new();

    [Fact]
    public void Reviewer_in_council_uses_four_scores_and_total_coefficient_six()
    {
        var result = _calculator.CalculateDefault(8m, 7m, 9m, 8m, reviewerIsCouncilMember: true);

        Assert.Equal(47m / 6m, result.FinalScore);
        Assert.Equal(6m, result.TotalWeight);
        Assert.Equal(4, result.Contributions.Count);
        Assert.Contains(result.Contributions, x => x.Code == "REVIEWER" && x.Weight == 2m);
        Assert.DoesNotContain(result.Contributions, x => x.Code == "COUNCIL_MEMBER");
    }

    [Fact]
    public void Reviewer_outside_council_uses_five_scores_and_total_coefficient_seven()
    {
        var result = _calculator.CalculateDefault(8m, 7m, 9m, 8m, reviewerIsCouncilMember: false, memberScore: 6m);

        Assert.Equal(53m / 7m, result.FinalScore);
        Assert.Equal(7m, result.TotalWeight);
    }

    [Fact]
    public void Rejected_advisor_score_removes_both_score_and_coefficient()
    {
        var result = _calculator.CalculateDefault(8m, 7m, 9m, 8m, reviewerIsCouncilMember: true,
            ignoredCodes: new HashSet<string> { "ADVISOR" });

        Assert.Equal(7.75m, result.FinalScore);
        Assert.Equal(4m, result.TotalWeight);
    }

    [Fact]
    public void Reviewer_serving_as_member_cannot_enter_a_second_council_score()
    {
        Assert.Throws<ArgumentException>(() =>
            _calculator.CalculateDefault(8m, 7m, 9m, 8m, reviewerIsCouncilMember: true, memberScore: 7m));
    }

    [Fact]
    public void Missing_external_council_member_score_is_not_treated_as_zero()
    {
        Assert.Throws<ArgumentException>(() =>
            _calculator.CalculateDefault(8m, 7m, 9m, 8m, reviewerIsCouncilMember: false));
    }

    [Fact]
    public void Custom_percentage_components_are_normalized_after_exclusion()
    {
        var result = _calculator.Calculate([
            new("ADVISOR", 8m, 40m, IsIgnoredInCalculation: true),
            new("REVIEWER", 7m, 40m),
            new("CUSTOM", 10m, 20m),
        ]);

        Assert.Equal(8m, result.FinalScore);
        Assert.Equal(60m, result.TotalWeight);
    }

    [Fact]
    public void A_large_difference_does_not_automatically_exclude_a_score()
    {
        var result = _calculator.Calculate([new("A", 2m, 1m), new("B", 10m, 1m)]);

        Assert.Equal(6m, result.FinalScore);
        Assert.All(result.Contributions, x => Assert.False(x.IsIgnoredInCalculation));
    }

    [Fact]
    public void All_excluded_scores_produce_no_valid_result()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _calculator.Calculate([new("A", 8m, 2m, IsIgnoredInCalculation: true)]));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(11, 1)]
    [InlineData(8, -1)]
    public void Invalid_score_or_weight_is_rejected(int score, int weight)
    {
        Assert.Throws<ArgumentException>(() => _calculator.Calculate([new("A", score, weight)]));
    }

    [Fact]
    public void Duplicate_contributions_are_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            _calculator.Calculate([new("A", 8m, 1m), new("A", 7m, 1m)]));
    }
}
