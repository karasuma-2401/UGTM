using System.Text.Json;
using System.Globalization;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

namespace UGTM.Modules.DefenseCouncil.Application.Grading;

public sealed record ConfiguredGradeSource(Guid Id, string Source, decimal? Score, bool Ignored, string? ExclusionReason);
public sealed record ResolvedGradeComponents(IReadOnlyList<ScoreComponentDto> Components, IReadOnlyList<object> Resolutions);

// Preview and official calculation must resolve the same configured sources, including
// applicability, council averages, exclusions and protection against counting a source twice.
public static class ConfiguredGradeComponents
{
    public static bool Applies(ScoreSummaryComponentDefinition component, bool reviewerInCouncil)
        => component.Applicability == "Always" || component.Applicability == (reviewerInCouncil ? "ReviewerInCouncil" : "ReviewerOutsideCouncil");

    public static Result<ResolvedGradeComponents> Resolve(ScoreSummaryConfigurationDto? config, bool reviewerInCouncil,
        IReadOnlyList<ConfiguredGradeSource> sources, bool includeExcluded = false)
    {
        Result<ResolvedGradeComponents> Fail(string code) => Result.Failure<ResolvedGradeComponents>(ScoreSummaryErrors.Conflict(code));
        if (config is null || !config.IsValid) return Fail("ConfigurationInvalid");
        var components = new List<ScoreComponentDto>();
        var resolutions = new List<object>();
        var used = new HashSet<Guid>();
        foreach (var c in config.Components.OrderBy(c => c.OrderIndex).ThenBy(c => c.Id))
        {
            var applies = Applies(c, reviewerInCouncil);
            if (!applies || (c.Weight == 0 && !c.IsRequired))
            {
                resolutions.Add(new object[] { c.Id.ToString("N"), applies, "Skipped" });
                continue;
            }
            decimal score;
            bool ignored;
            string? reason;
            Guid[] sourceIds;
            if (c.Source == "CouncilAverage")
            {
                var council = sources.Where(s => s.Source.StartsWith("Council", StringComparison.Ordinal)).ToArray();
                if (council.Length == 0 || council.Any(s => !s.Score.HasValue)) return Fail("MissingRequiredSource");
                var accepted = council.Where(s => includeExcluded || !s.Ignored).ToArray();
                if (accepted.Length == 0) return Fail("NoPositiveWeight");
                score = accepted.Average(s => s.Score!.Value);
                ignored = false;
                reason = null;
                sourceIds = council.Select(s => s.Id).Order().ToArray();
            }
            else if (c.Source is "Advisor" or "Reviewer" or "CouncilChairman" or "CouncilSecretary" or "CouncilMember")
            {
                var candidates = sources.Where(s => s.Source == c.Source).ToArray();
                if (candidates.Length > 1) return Fail("AmbiguousSource");
                if (candidates.Length == 0 || !candidates[0].Score.HasValue) return Fail("MissingRequiredSource");
                var source = candidates[0];
                score = source.Score!.Value;
                ignored = !includeExcluded && source.Ignored;
                reason = source.ExclusionReason;
                sourceIds = [source.Id];
            }
            else return Fail("UnsupportedSource");

            if (sourceIds.Any(id => !used.Add(id))) return Fail("AmbiguousSource");
            var refs = JsonSerializer.Serialize(sourceIds.Select(id => id.ToString("N")).ToArray());
            components.Add(new(c.Code, c.Name, c.Source, score, c.Weight, ignored, reason, refs));
            resolutions.Add(new object[] { c.Id.ToString("N"), true, refs, score.ToString("G29", CultureInfo.InvariantCulture), ignored, reason ?? "" });
        }
        return Result.Success(new ResolvedGradeComponents(components, resolutions));
    }
}
