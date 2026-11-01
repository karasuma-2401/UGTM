using UGTM.Modules.AcademicPeriods.Contracts.ReviewerEvaluations;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public enum ReviewerDefenseRuleOutcome { Succeeded, ConfigurationMissing, ConfigurationInvalid, ConfigurationAmbiguous }
public sealed record ReviewerDefenseRuleResolution(ReviewerDefenseRuleOutcome Outcome, decimal? MinimumScore = null);

public static class ReviewerDefenseRuleResolver
{
    public static ReviewerDefenseRuleResolution Resolve(ReviewerDefenseRuleContractDto? rule)
    {
        if (rule is null) return new(ReviewerDefenseRuleOutcome.ConfigurationMissing);
        if (rule.Outcome == ReviewerDefenseRuleReadOutcome.ConfigurationInvalid) return new(ReviewerDefenseRuleOutcome.ConfigurationInvalid);
        if (rule.Outcome == ReviewerDefenseRuleReadOutcome.ConfigurationMissing) return new(ReviewerDefenseRuleOutcome.ConfigurationMissing);
        if (rule.Outcome != ReviewerDefenseRuleReadOutcome.Succeeded || rule.BatchId == Guid.Empty
            || !rule.ConfigurationId.HasValue || rule.ConfigurationId == Guid.Empty || rule.ConfigurationVersionNo is not > 0
            || rule.Components.Any(value => value.Id == Guid.Empty || value.MinimumScore is < 0m or > 10m
                || value.Applicability is not ("Always" or "ReviewerInCouncil" or "ReviewerOutsideCouncil"))
            || rule.Components.Select(value => value.Id).Distinct().Count() != rule.Components.Count)
            return new(ReviewerDefenseRuleOutcome.ConfigurationInvalid);
        if (rule.Components.Count == 0 || rule.Components.Any(value => value.MinimumScore is null)
            || !rule.Components.Any(value => value.Applicability is "Always" or "ReviewerInCouncil")
            || !rule.Components.Any(value => value.Applicability is "Always" or "ReviewerOutsideCouncil"))
            return new(ReviewerDefenseRuleOutcome.ConfigurationMissing);
        var minimums = rule.Components.Select(value => value.MinimumScore!.Value).Distinct().ToArray();
        return minimums.Length == 1 ? new(ReviewerDefenseRuleOutcome.Succeeded, minimums[0])
            : new(ReviewerDefenseRuleOutcome.ConfigurationAmbiguous);
    }
}
