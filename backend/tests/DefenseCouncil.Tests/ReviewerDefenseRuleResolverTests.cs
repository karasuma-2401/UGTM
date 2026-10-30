using UGTM.Modules.AcademicPeriods.Contracts.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class ReviewerDefenseRuleResolverTests
{
    [Theory]
    [InlineData("Always:5", ReviewerDefenseRuleOutcome.Succeeded, 5)]
    [InlineData("ReviewerInCouncil:5,ReviewerOutsideCouncil:5", ReviewerDefenseRuleOutcome.Succeeded, 5)]
    [InlineData("Always:5,ReviewerInCouncil:5", ReviewerDefenseRuleOutcome.Succeeded, 5)]
    [InlineData("Always:5,Always:5", ReviewerDefenseRuleOutcome.Succeeded, 5)]
    [InlineData("Always:0", ReviewerDefenseRuleOutcome.Succeeded, 0)]
    [InlineData("Always:10", ReviewerDefenseRuleOutcome.Succeeded, 10)]
    [InlineData("ReviewerInCouncil:5", ReviewerDefenseRuleOutcome.ConfigurationMissing, null)]
    [InlineData("Always:null", ReviewerDefenseRuleOutcome.ConfigurationMissing, null)]
    [InlineData("Always:5,ReviewerOutsideCouncil:null", ReviewerDefenseRuleOutcome.ConfigurationMissing, null)]
    [InlineData("ReviewerInCouncil:5,ReviewerOutsideCouncil:6", ReviewerDefenseRuleOutcome.ConfigurationAmbiguous, null)]
    [InlineData("Always:5,ReviewerOutsideCouncil:6", ReviewerDefenseRuleOutcome.ConfigurationAmbiguous, null)]
    [InlineData("Always:11,ReviewerOutsideCouncil:null", ReviewerDefenseRuleOutcome.ConfigurationInvalid, null)]
    [InlineData("Always:-1", ReviewerDefenseRuleOutcome.ConfigurationInvalid, null)]
    [InlineData("Unknown:5", ReviewerDefenseRuleOutcome.ConfigurationInvalid, null)]
    [InlineData("", ReviewerDefenseRuleOutcome.ConfigurationMissing, null)]
    public void Resolve_covers_both_future_council_branches_without_guessing(string definitions, ReviewerDefenseRuleOutcome outcome, int? minimum)
    {
        var components = definitions.Length == 0 ? [] : definitions.Split(',').Select(definition =>
        {
            var parts = definition.Split(':');
            return new ReviewerDefenseRuleComponentContractDto(Guid.NewGuid(), parts[0], parts[1] == "null" ? null
                : decimal.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
        }).ToArray();
        var result = ReviewerDefenseRuleResolver.Resolve(Rule(components));
        Assert.Equal(outcome, result.Outcome); Assert.Equal(minimum.HasValue ? (decimal?)minimum.Value : null, result.MinimumScore);
    }

    [Fact]
    public void Metadata_and_reader_outcomes_fail_closed_without_defaulting_to_five()
    {
        var component = new ReviewerDefenseRuleComponentContractDto(Guid.NewGuid(), "Always", 5m);
        var valid = Rule([component]);
        Assert.Equal(ReviewerDefenseRuleOutcome.ConfigurationMissing, ReviewerDefenseRuleResolver.Resolve(null).Outcome);
        Assert.Equal(ReviewerDefenseRuleOutcome.ConfigurationMissing, ReviewerDefenseRuleResolver.Resolve(valid with { Outcome = ReviewerDefenseRuleReadOutcome.ConfigurationMissing }).Outcome);
        foreach (var invalid in new[]
        {
            valid with { BatchId = Guid.Empty }, valid with { ConfigurationId = null }, valid with { ConfigurationVersionNo = 0 },
            valid with { Components = [component, component] }, valid with { Components = [component with { Id = Guid.Empty }] },
            valid with { Outcome = ReviewerDefenseRuleReadOutcome.ConfigurationInvalid }
        }) Assert.Equal(ReviewerDefenseRuleOutcome.ConfigurationInvalid, ReviewerDefenseRuleResolver.Resolve(invalid).Outcome);
        // Rule identity/version follow Active, while the finalized evaluation remains independent.
        Assert.Equal(6m, ReviewerDefenseRuleResolver.Resolve(valid with
        {
            ConfigurationId = Guid.NewGuid(),
            ConfigurationVersionNo = 2,
            Components = [component with { MinimumScore = 6m }]
        }).MinimumScore);
    }
    private static ReviewerDefenseRuleContractDto Rule(IReadOnlyList<ReviewerDefenseRuleComponentContractDto> components)
        => new(Guid.NewGuid(), "Open", Guid.NewGuid(), 1, ReviewerDefenseRuleReadOutcome.Succeeded, components);
}
