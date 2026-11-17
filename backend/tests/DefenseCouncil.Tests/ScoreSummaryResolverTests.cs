using UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class ScoreSummaryResolverTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 4, 0, 0, DateTimeKind.Utc);
    private sealed record Input(ReviewerAssignmentThesisContractDto Group, ReviewerAssignmentTopicContractDto Topic, ScoreFacts Facts, FinalSubmissionArchiveProofContractDto Archive, ScoreSummaryConfigurationDto Config);
    private static void Set(object target, string property, object? value) => target.GetType().GetProperty(property)!.SetValue(target, value);
    private static Input Valid(bool inside = false)
    {
        var group = Guid.NewGuid(); var topic = Guid.NewGuid(); var batch = Guid.NewGuid(); var advisorId = Guid.NewGuid(); var reviewerId = Guid.NewGuid(); var report = Guid.NewGuid(); var file = Guid.NewGuid(); var sha = new string('a', 64);
        var a = AdvisorEvaluation.Create(Guid.NewGuid(), group, report, 1, file, sha, 8, "advisor", advisorId, Now);
        a.TryDecide(true, null, advisorId, Now); Set(a, "Version", 1u);
        var r = ReviewerAssignment.Create(Guid.NewGuid(), group, reviewerId, advisorId, a.Id, report, 1, file, sha, advisorId, Now); Set(r, "Version", 2u);
        var e = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), r.Id, 7, "reviewer", DefenseApprovalStatus.Approved, reviewerId, Now);
        e.TrySubmit(7, "reviewer", DefenseApprovalStatus.Approved, reviewerId, Now); Set(e, "Version", 3u);
        var c = new Council { Id = Guid.NewGuid(), BatchId = batch, Status = CouncilStatus.Active };
        var ca = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = c.Id, GroupId = group, Version = 4 };
        var members = new[] { CouncilMemberRole.Chairman, CouncilMemberRole.Secretary, CouncilMemberRole.Member }.Select(role => new CouncilMember { Id = Guid.NewGuid(), CouncilId = c.Id, UserId = inside && role == CouncilMemberRole.Member ? reviewerId : Guid.NewGuid(), RoleInCouncil = role, Version = 5 }).ToArray();
        var scores = members.Where(m => m.UserId != reviewerId).Select(m => new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = ca.Id, CouncilMemberId = m.Id, Score = m.RoleInCouncil == CouncilMemberRole.Chairman ? 9 : m.RoleInCouncil == CouncilMemberRole.Secretary ? 8 : 6, EvaluatedAt = Now, Version = 6 }).ToArray();
        var details = scores.Select(s => new EvaluationDetail { Id = Guid.NewGuid(), EvaluationId = s.Id, CriteriaId = Guid.NewGuid(), Score = s.Score }).ToArray();
        var minute = new CouncilMinute { Id = Guid.NewGuid(), CouncilAssignmentId = ca.Id, LockStatus = CouncilMinuteLockStatus.Locked, LockedBy = advisorId, LockedAt = Now, Version = 7 };
        var components = new[] { ("arbitrary-advisor-label", "Advisor", 2m, "Always"), ("r", "Reviewer", 2m, "Always"), ("c", "CouncilChairman", 1m, "Always"), ("s", "CouncilSecretary", 1m, "Always"), ("m", "CouncilMember", 1m, "ReviewerOutsideCouncil") }
            .Select((v, i) => new ScoreSummaryComponentDefinition(Guid.NewGuid(), v.Item1, v.Item1, v.Item2, v.Item4, v.Item3, true, 10, true, i + 1)).ToArray();
        return new(new(group, topic, batch, false, report, true, 1, file, sha), new(topic, batch, advisorId, "Topic", null), new([a], [r], [e], [ca], [c], members, [minute], scores, details),
            new(ArchiveProofOutcome.Succeeded, group, batch, Guid.NewGuid(), 8, Guid.NewGuid(), 1, sha, Guid.NewGuid(), Guid.NewGuid(), advisorId, Now, advisorId, Now, []),
            new(Guid.NewGuid(), batch, 1, 9, "IntegerCoefficient", true, components));
    }
    private static UGTM.BuildingBlocks.Domain.Result<ResolvedScoreCalculation> Resolve(Input i) => new ScoreSummaryResolver(new ThesisGradeCalculator()).Resolve(i.Group, i.Topic, i.Facts, i.Archive, i.Config, Now);

    [Theory]
    [InlineData("dynamic", false)]
    [InlineData("dynamic", true)]
    [InlineData("percentage", false)]
    [InlineData("average", false)]
    [InlineData("average", true)]
    [InlineData("midpoint", false)]
    public void Minute_preview_matches_official_configured_calculation(string mode, bool inside)
    {
        var i = Valid(inside);
        i = i with { Config = i.Config with { VersionNo = 17, Components = i.Config.Components.Select(c => c.Source == "Advisor" ? c with { Weight = 7 } : c).ToArray() } };
        if (mode == "percentage")
            i = i with { Config = i.Config with { Method = "Percentage", Components = i.Config.Components.Select(c => c with { Weight = 20 }).ToArray() } };
        if (mode == "average")
        {
            i.Facts.Scores[0].IsIgnoredInCalculation = true;
            i.Facts.Scores[0].ExclusionReason = "decision";
            i = i with { Config = i.Config with { Components = [i.Config.Components[0], i.Config.Components[1], i.Config.Components[2] with { Source = "CouncilAverage" }] } };
        }
        if (mode == "midpoint")
        {
            Set(i.Facts.Evaluations[0], "Score", 7.69m);
            i = i with { Config = i.Config with { Components = [i.Config.Components[0] with { Weight = 1 }, i.Config.Components[1] with { Weight = 1 }] } };
        }
        var a = i.Facts.Advisors[0]; var e = i.Facts.Evaluations[0];
        var items = new List<CouncilMinuteWorkspaceCalculator.ItemEvaluationData>
        {
            new("ADVISOR", "GVHD", "Advisor", a.Score, 0, "", false, a.IsIgnoredInCalculation, a.ExclusionReason, a.Id),
            new("REVIEWER", "GVPB", "Reviewer", e.Score, 0, "", inside, e.IsIgnoredInCalculation, e.ExclusionReason, e.Id)
        };
        foreach (var member in i.Facts.Members)
        {
            var score = i.Facts.Scores.SingleOrDefault(s => s.CouncilMemberId == member.Id);
            var code = member.RoleInCouncil == CouncilMemberRole.Chairman ? "COUNCIL_CHAIRMAN" : member.RoleInCouncil == CouncilMemberRole.Secretary ? "COUNCIL_SECRETARY" : "COUNCIL_MEMBER";
            items.Add(new(code, code, "Member", score?.Score, 0, "", member.UserId == i.Facts.Reviewers[0].ReviewerId,
                score?.IsIgnoredInCalculation ?? false, score?.ExclusionReason, score?.Id ?? member.Id));
        }
        var official = Resolve(i);
        var (_, _, preview) = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(items, null, null, null, "ACCEPTED_AS_PER_MINUTE", i.Config);
        Assert.True(official.IsSuccess);
        Assert.Null(preview.CalculationErrorCode);
        Assert.Equal(official.Value.Final, preview.FinalRoundedScore);
        Assert.Equal(official.Value.TotalWeight, preview.AdjustedDivisor);
        Assert.Equal(Math.Round(official.Value.Raw, 6), preview.AdjustedAverage);
        Assert.Equal(17, preview.ConfigurationVersion);
        Assert.True(preview.IsReadyToLock);
    }
    [Theory]
    [InlineData(false, 7.57, 7)]
    [InlineData(true, 7.83, 6)]
    public void ScoreSummary_resolver_uses_source_applicability_and_does_not_apply_minimum_score_gate(bool inside, double score, int weight)
    {
        var result = Resolve(Valid(inside)); Assert.True(result.IsSuccess); Assert.Equal((decimal)score, result.Value.Final); Assert.Equal(weight, result.Value.TotalWeight);
    }
    [Theory]
    [InlineData(false, 7.6, 100)]
    [InlineData(true, 8.0, 80)]
    public void ScoreSummary_percentage_normalizes_only_accepted_weights(bool ignored, double expected, int weight)
    {
        var i = Valid(); i = i with { Config = i.Config with { Method = "Percentage", Components = i.Config.Components.Select(c => c with { Weight = 20, Applicability = "Always" }).ToArray() } };
        if (ignored) { i.Facts.Scores[2].IsIgnoredInCalculation = true; i.Facts.Scores[2].ExclusionReason = "committee decision"; }
        var result = Resolve(i); Assert.True(result.IsSuccess); Assert.Equal((decimal)expected, result.Value.Final); Assert.Equal(weight, result.Value.TotalWeight);
    }
    [Fact]
    public void ScoreSummary_dynamic_nondefault_weights_and_single_rounding_are_preserved()
    {
        var i = Valid(); i = i with { Config = i.Config with { Components = i.Config.Components.Select(c => c.Source == "Advisor" ? c with { Weight = 7 } : c).ToArray() } };
        var result = Resolve(i); Assert.True(result.IsSuccess); Assert.Equal(93m / 12m, result.Value.Raw); Assert.Equal(7.75m, result.Value.Final);
    }
    [Fact]
    public void ScoreSummary_council_average_uses_raw_decimal_and_excludes_ignored_scores_before_average()
    {
        var i = Valid(); i.Facts.Scores[2].IsIgnoredInCalculation = true; i.Facts.Scores[2].ExclusionReason = "decision";
        i = i with { Config = i.Config with { Components = [i.Config.Components[0], i.Config.Components[1], i.Config.Components[2] with { Source = "CouncilAverage" }] } };
        var result = Resolve(i); Assert.True(result.IsSuccess); Assert.Equal(8.5m, result.Value.Components.Single(c => c.Source == "CouncilAverage").Score);
        Assert.Equal(38.5m / 5m, result.Value.Raw);
    }
    [Theory]
    [InlineData("custom", "UnsupportedSource")]
    [InlineData("duplicate", "AmbiguousSource")]
    [InlineData("overlap", "AmbiguousSource")]
    [InlineData("members", "AmbiguousSource")]
    [InlineData("allIgnored", "NoPositiveWeight")]
    [InlineData("invalidConfig", "ConfigurationInvalid")]
    [InlineData("legacyAdvisor", "MissingRequiredSource")]
    [InlineData("draftReviewer", "MissingRequiredSource")]
    [InlineData("reportBinding", "MissingRequiredSource")]
    [InlineData("noScore", "MissingRequiredSource")]
    [InlineData("noDetail", "MissingRequiredSource")]
    [InlineData("wrongAssignment", "MissingRequiredSource")]
    [InlineData("unlocked", "NotEligible")]
    [InlineData("noArchive", "NotEligible")]
    [InlineData("reviewerChair", "ContextInvalid")]
    [InlineData("advisorMember", "ContextInvalid")]
    [InlineData("badExclusion", "ContextInvalid")]
    public void ScoreSummary_resolver_fails_closed_on_incomplete_or_ambiguous_inputs(string kind, string error)
    {
        var i = Valid();
        switch (kind)
        {
            case "custom": i = i with { Config = i.Config with { Components = [i.Config.Components[0] with { Source = "Custom" }] } }; break;
            case "duplicate": i = i with { Config = i.Config with { Components = [i.Config.Components[0], i.Config.Components[1] with { Source = "Advisor" }] } }; break;
            case "overlap": i = i with { Config = i.Config with { Components = [i.Config.Components[2] with { Source = "CouncilAverage" }, i.Config.Components[3]] } }; break;
            case "members":
                var member = new CouncilMember { Id = Guid.NewGuid(), CouncilId = i.Facts.Councils[0].Id, RoleInCouncil = CouncilMemberRole.Member, UserId = Guid.NewGuid(), Version = 4 };
                var score = new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = i.Facts.Assignments[0].Id, CouncilMemberId = member.Id, Score = 5, EvaluatedAt = Now, Version = 4, IsIgnoredInCalculation = true, ExclusionReason = "ignored" };
                i = i with { Facts = i.Facts with { Members = [.. i.Facts.Members, member], Scores = [.. i.Facts.Scores, score], Details = [.. i.Facts.Details, new EvaluationDetail { Id = Guid.NewGuid(), EvaluationId = score.Id, CriteriaId = Guid.NewGuid(), Score = 5 }] } }; break;
            case "allIgnored":
                Set(i.Facts.Advisors[0], "IsIgnoredInCalculation", true); Set(i.Facts.Advisors[0], "ExclusionReason", "ignored");
                Set(i.Facts.Evaluations[0], "IsIgnoredInCalculation", true); Set(i.Facts.Evaluations[0], "ExclusionReason", "ignored");
                foreach (var s in i.Facts.Scores) { s.IsIgnoredInCalculation = true; s.ExclusionReason = "ignored"; }
                break;
            case "invalidConfig": i = i with { Config = i.Config with { IsValid = false } }; break;
            case "legacyAdvisor": Set(i.Facts.Advisors[0], "IsLegacy", true); break;
            case "draftReviewer": Set(i.Facts.Evaluations[0], "Status", ReviewerEvaluationStatus.Draft); break;
            case "reportBinding": i = i with { Group = i.Group with { ReportVersionNo = 2 } }; break;
            case "noScore": i = i with { Facts = i.Facts with { Scores = [] } }; break;
            case "noDetail": i = i with { Facts = i.Facts with { Details = [] } }; break;
            case "wrongAssignment": i.Facts.Scores[0].CouncilAssignmentId = Guid.NewGuid(); break;
            case "unlocked": i.Facts.Minutes[0].LockStatus = CouncilMinuteLockStatus.AllScoresEntered; break;
            case "noArchive": i = i with { Archive = i.Archive with { Outcome = ArchiveProofOutcome.Legacy } }; break;
            case "reviewerChair": i.Facts.Members[0].UserId = i.Facts.Reviewers[0].ReviewerId; break;
            case "advisorMember": i.Facts.Members[2].UserId = i.Topic.AdvisorId; break;
            case "badExclusion": i.Facts.Scores[0].IsIgnoredInCalculation = true; break;
        }
        var result = Resolve(i); Assert.True(result.IsFailure); Assert.Equal("ScoreSummary." + error, result.Error.Code);
    }
    [Fact]
    public void ScoreSummary_snapshot_order_is_canonical_and_same_score_with_new_source_version_is_stale()
    {
        var i = Valid(); var first = Resolve(i).Value;
        var reordered = i with { Facts = i.Facts with { Members = i.Facts.Members.Reverse().ToArray(), Scores = i.Facts.Scores.Reverse().ToArray(), Details = i.Facts.Details.Reverse().ToArray() } };
        Assert.Equal(first.Fingerprint, Resolve(reordered).Value.Fingerprint);
        i.Facts.Scores[0].Version++; Assert.NotEqual(first.Fingerprint, Resolve(i).Value.Fingerprint);
    }
    [Fact]
    public void ScoreSummary_optional_custom_in_non_applicable_branch_is_not_resolved()
    {
        var i = Valid(true); i = i with { Config = i.Config with { Components = [.. i.Config.Components, i.Config.Components[4] with { Id = Guid.NewGuid(), Code = "custom", Source = "Custom", Applicability = "ReviewerOutsideCouncil" }] } };
        Assert.True(Resolve(i).IsSuccess);
    }
    [Fact]
    public void ScoreSummary_future_minute_cannot_attest_a_completed_defense()
    {
        var i = Valid(); i.Facts.Minutes[0].LockedAt = Now.AddDays(1);
        Assert.Equal("ScoreSummary.NotEligible", Resolve(i).Error.Code);
    }
    [Fact]
    public void ScoreSummary_rounds_only_final_midpoint_away_from_zero()
    {
        var i = Valid(); Set(i.Facts.Evaluations[0], "Score", 7.69m);
        i = i with { Config = i.Config with { Components = [i.Config.Components[0] with { Weight = 1 }, i.Config.Components[1] with { Weight = 1 }] } };
        var result = Resolve(i); Assert.True(result.IsSuccess); Assert.Equal(7.845m, result.Value.Raw); Assert.Equal(7.85m, result.Value.Final);
    }
    [Fact]
    public void ScoreSummary_completed_rejected_reviewer_is_not_reused_as_a_new_defense_gate()
    {
        var i = Valid(); Set(i.Facts.Evaluations[0], "DefenseApproval", DefenseApprovalStatus.Rejected);
        Assert.True(Resolve(i).IsSuccess);
    }
}
