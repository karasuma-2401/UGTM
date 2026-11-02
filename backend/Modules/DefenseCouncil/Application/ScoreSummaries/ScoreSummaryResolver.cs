using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

public sealed record ResolvedScoreCalculation(decimal Raw, decimal Final, decimal TotalWeight, string Formula,
    string ConfigSnapshot, string SourceSnapshot, string Fingerprint, IReadOnlyList<ScoreComponentDto> Components,
    decimal AdvisorScore, decimal ReviewerScore, decimal? CouncilScore);

public sealed class ScoreSummaryResolver(ThesisGradeCalculator calculator)
{
    public static string N(Guid id) => id.ToString("N");
    public static string D(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
    public static string T(DateTime value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
    public Result<ResolvedScoreCalculation> Resolve(ReviewerAssignmentThesisContractDto group, ReviewerAssignmentTopicContractDto topic,
        ScoreFacts facts, FinalSubmissionArchiveProofContractDto archive, ScoreSummaryConfigurationDto? config, DateTime asOfUtc)
    {
        Result<ResolvedScoreCalculation> Fail(string code) => Result.Failure<ResolvedScoreCalculation>(ScoreSummaryErrors.Conflict(code));
        if (config is null || !config.IsValid || config.BatchId != topic.BatchId) return Fail("ConfigurationInvalid");
        if (archive.Outcome != ArchiveProofOutcome.Succeeded || archive.GroupId != group.GroupId || archive.BatchId != topic.BatchId) return Fail("NotEligible");
        var sourceValidation = ValidateSources(group, topic, facts);
        if (sourceValidation.IsFailure) return Fail(sourceValidation.Error.Code.Split('.').Last());
        var (a, r, e, assignment, council, minute, members, required, inside) = sourceValidation.Value;
        if (asOfUtc.Kind != DateTimeKind.Utc || minute.LockedAt!.Value.Kind != DateTimeKind.Utc || minute.LockedAt > asOfUtc
            || archive.ArchivedConfirmedAtUtc > asOfUtc || a.DecisionAt > minute.LockedAt || e.SubmittedAtUtc > minute.LockedAt
            || facts.Scores.Any(s => s.EvaluatedAt.Kind != DateTimeKind.Utc || s.EvaluatedAt > minute.LockedAt)) return Fail("NotEligible");
        var reviewerScore = e.Score ?? throw new InvalidOperationException("Validated reviewer source has no score.");
        var components = new List<ScoreComponentDto>(); var used = new HashSet<Guid>(); var resolutions = new List<object>();
        foreach (var c in config.Components.OrderBy(c => c.OrderIndex).ThenBy(c => c.Id))
        {
            var applies = c.Applicability == "Always" || c.Applicability == (inside ? "ReviewerInCouncil" : "ReviewerOutsideCouncil");
            if (!applies || (c.Weight == 0 && !c.IsRequired)) { resolutions.Add(new object[] { N(c.Id), applies, "Skipped" }); continue; }
            decimal score; bool ignored; string? reason; Guid[] sourceIds;
            if (c.Source == "Advisor") { score = a.Score; ignored = a.IsIgnoredInCalculation; reason = a.ExclusionReason; sourceIds = [a.Id]; }
            else if (c.Source == "Reviewer") { score = reviewerScore; ignored = e.IsIgnoredInCalculation; reason = e.ExclusionReason; sourceIds = [e.Id]; }
            else if (c.Source == "CouncilAverage")
            {
                var accepted = facts.Scores.Where(s => !s.IsIgnoredInCalculation).ToArray();
                if (accepted.Length == 0) return Fail("NoPositiveWeight");
                score = accepted.Average(s => s.Score); ignored = false; reason = null; sourceIds = facts.Scores.Select(s => s.Id).Order().ToArray();
            }
            else if (c.Source is "CouncilChairman" or "CouncilSecretary" or "CouncilMember")
            {
                var role = c.Source == "CouncilChairman" ? CouncilMemberRole.Chairman : c.Source == "CouncilSecretary" ? CouncilMemberRole.Secretary : CouncilMemberRole.Member;
                var candidates = required.Where(m => m.RoleInCouncil == role).ToArray();
                if (candidates.Length > 1) return Fail("AmbiguousSource");
                if (candidates.Length == 0) return Fail("MissingRequiredSource");
                var s = facts.Scores.Single(s => s.CouncilMemberId == candidates[0].Id);
                score = s.Score; ignored = s.IsIgnoredInCalculation; reason = s.ExclusionReason; sourceIds = [s.Id];
            }
            else return Fail("UnsupportedSource");
            if (sourceIds.Any(id => !used.Add(id))) return Fail("AmbiguousSource");
            var refs = JsonSerializer.Serialize(sourceIds.Select(N).ToArray());
            components.Add(new(c.Code, c.Name, c.Source, score, c.Weight, ignored, reason, refs));
            resolutions.Add(new object[] { N(c.Id), true, refs, D(score), ignored, reason ?? "" });
        }
        try
        {
            if (!components.Any(c => !c.IsIgnoredInCalculation && c.Weight > 0)) return Fail("NoPositiveWeight");
            var result = calculator.Calculate(components.Select(c => new GradeContribution(c.ComponentCode, c.Score, c.Weight, c.IsIgnoredInCalculation)));
            if (result.TotalWeight > 99999999.9999m || components.Any(c => c.Weight > 99999.9999m)) return Fail("ConfigurationInvalid");
            var configSnapshot = JsonSerializer.Serialize(new object[]{N(config.Id),N(config.BatchId),config.VersionNo,config.Version,config.Method,
                config.Components.OrderBy(c=>c.OrderIndex).ThenBy(c=>c.Id).Select(c=>new object[]{N(c.Id),c.Code,c.Name,c.Source,c.Applicability,D(c.Weight),c.IsRequired,c.MinimumScore is null?"":D(c.MinimumScore.Value),c.RequiresApproval,c.OrderIndex}).ToArray(),resolutions});
            var sourceSnapshot = JsonSerializer.Serialize(new object[]{"ThesisScoreInputs:v1",N(group.GroupId),N(topic.TopicId),N(topic.BatchId),N(topic.AdvisorId),topic.TitleVi,topic.TitleEn??"",
                new object?[]{N(a.Id),a.Version,a.FinalReportId?.ToString("N"),a.ReportVersionNo,a.SubmissionFileId?.ToString("N"),a.SubmissionChecksumSha256,D(a.Score),a.IsIgnoredInCalculation,a.ExclusionReason,a.AllowsReviewerAssignment,a.DecisionBy?.ToString("N"),T(a.DecisionAt!.Value)},
                new object?[]{N(r.Id),r.Version,N(r.ReviewerId),r.AdvisorEvaluationId?.ToString("N"),r.AdvisorIdAtAssignment?.ToString("N"),r.FinalReportId?.ToString("N"),r.ReportVersionNo,r.SubmissionFileId?.ToString("N"),r.SubmissionChecksumSha256,N(e.Id),e.Version,e.Status.ToString(),e.DefenseApproval.ToString(),D(reviewerScore),e.IsIgnoredInCalculation,e.ExclusionReason,N(e.SubmittedBy!.Value),T(e.SubmittedAtUtc!.Value)},
                new object[]{N(council.Id),N(assignment.Id),assignment.Version,N(minute.Id),minute.Version,minute.LockStatus.ToString(),N(minute.LockedBy!.Value),T(minute.LockedAt!.Value)},
                members.Select(m=>new object[]{N(m.Id),m.Version,N(m.UserId),m.RoleInCouncil.ToString()}).ToArray(),
                facts.Scores.OrderBy(s=>s.Id).Select(s=>new object?[]{N(s.Id),s.Version,N(s.CouncilMemberId),D(s.Score),s.IsIgnoredInCalculation,s.ExclusionReason,T(s.EvaluatedAt),facts.Details.Where(d=>d.EvaluationId==s.Id).OrderBy(d=>d.Id).Select(d=>new object[]{N(d.Id),N(d.CriteriaId),D(d.Score)}).ToArray()}).ToArray(),
                new object?[]{archive.SubmissionId?.ToString("N"),archive.SubmissionVersion,archive.PackageId?.ToString("N"),archive.PackageVersionNo,archive.ManifestSha256,archive.AdvisorReviewId?.ToString("N"),archive.FacultyReviewId?.ToString("N"),archive.AdvisorConfirmedBy?.ToString("N"),archive.AdvisorConfirmedAtUtc is {} at?T(at):null,archive.ArchivedConfirmedBy?.ToString("N"),archive.ArchivedConfirmedAtUtc is {} ft?T(ft):null,
                    archive.Files.OrderBy(f=>f.FileRole,StringComparer.Ordinal).Select(f=>new object[]{N(f.FileId),f.FileRole,f.ContentType,f.SizeBytes,f.ChecksumSha256}).ToArray()}});
            var formula = "(" + string.Join(" + ", components.Where(c => !c.IsIgnoredInCalculation).Select(c => $"{D(c.Weight)}*{c.ComponentCode}")) + ") / " + D(result.TotalWeight);
            if (formula.Length > 200) formula = "SUM(score*weight) / SUM(weight); see revision components";
            return Result.Success(new ResolvedScoreCalculation(result.FinalScore, decimal.Round(result.FinalScore, 2, MidpointRounding.AwayFromZero), result.TotalWeight, formula, configSnapshot, sourceSnapshot,
                Hash(new object[] { "ThesisScore:v1", "AwayFromZero", 2, sourceSnapshot, configSnapshot }), components, a.Score, reviewerScore, facts.Scores.Where(s => !s.IsIgnoredInCalculation).Select(s => (decimal?)s.Score).Average()));
        }
        catch (InvalidOperationException) { return Fail("NoPositiveWeight"); }
        catch (Exception error) when (error is ArgumentException or OverflowException) { return Fail("ConfigurationInvalid"); }
    }
    public sealed record ValidatedSources(AdvisorEvaluation Advisor, ReviewerAssignment Reviewer, ReviewerEvaluation Evaluation, CouncilAssignment Assignment,
        Council Council, CouncilMinute Minute, CouncilMember[] Members, CouncilMember[] Required, bool Inside);
    public Result<ValidatedSources> ValidateSources(ReviewerAssignmentThesisContractDto group, ReviewerAssignmentTopicContractDto topic, ScoreFacts facts, bool requireLocked = true)
    {
        Result<ValidatedSources> Fail(string code) => Result.Failure<ValidatedSources>(ScoreSummaryErrors.Conflict(code));
        if (group.BatchId != topic.BatchId || group.TopicId != topic.TopicId || group.FinalReportId is null || group.ReportVersionNo is not > 0
            || group.SubmissionFileId is null || !Sha(group.SubmissionChecksumSha256)) return Fail("ContextInvalid");
        if (facts.Advisors.Count != 1 || facts.Reviewers.Count != 1 || facts.Evaluations.Count != 1 || facts.Assignments.Count != 1 || facts.Councils.Count != 1 || facts.Minutes.Count != 1)
            return Fail("MissingRequiredSource");
        var a = facts.Advisors[0]; var r = facts.Reviewers[0]; var e = facts.Evaluations[0]; var assignment = facts.Assignments[0]; var council = facts.Councils[0]; var minute = facts.Minutes[0];
        if (!a.IsLocked || a.Version == 0 || a.FinalReportId != group.FinalReportId || a.ReportVersionNo != group.ReportVersionNo
            || a.SubmissionFileId != group.SubmissionFileId || a.SubmissionChecksumSha256 != group.SubmissionChecksumSha256
            || !Id(a.EvaluatedBy) || !Id(a.DecisionBy) || a.DecisionAt is null || a.Score is < 0 or > 10
            || r.IsLegacy || !r.IsActive || r.AssignmentOrder != 1 || r.Version == 0 || r.AdvisorIdAtAssignment != topic.AdvisorId || r.AdvisorEvaluationId != a.Id
            || r.FinalReportId != a.FinalReportId || r.ReportVersionNo != a.ReportVersionNo || r.SubmissionFileId != a.SubmissionFileId || r.SubmissionChecksumSha256 != a.SubmissionChecksumSha256
            || e.IsLegacy || e.Status != ReviewerEvaluationStatus.Submitted || e.AssignmentId != r.Id || e.Score is null or < 0 or > 10
            || e.Version == 0 || !Id(e.SubmittedBy) || e.SubmittedAtUtc is null || e.DefenseApproval is null || !Enum.IsDefined(e.DefenseApproval.Value))
            return Fail("MissingRequiredSource");
        if (assignment.GroupId != group.GroupId || assignment.CouncilId != council.Id || council.BatchId != topic.BatchId
            || council.Status is not (CouncilStatus.Active or CouncilStatus.Completed)
            || minute.CouncilAssignmentId != assignment.Id || (requireLocked && minute.LockStatus != CouncilMinuteLockStatus.Locked)
            || (requireLocked && (!Id(minute.LockedBy) || minute.LockedAt is null)) || minute.Version == 0) return Fail("NotEligible");
        var members = facts.Members.OrderBy(m => m.Id).ToArray();
        if (members.Length < 2 || members.Any(m => m.CouncilId != council.Id || m.Id == Guid.Empty || m.UserId == Guid.Empty || !Enum.IsDefined(m.RoleInCouncil))
            || members.Select(m => m.UserId).Distinct().Count() != members.Length || members.Count(m => m.RoleInCouncil == CouncilMemberRole.Chairman) != 1
            || members.Count(m => m.RoleInCouncil == CouncilMemberRole.Secretary) != 1 || members.Any(m => m.UserId == topic.AdvisorId)
            || members.Any(m => m.UserId == r.ReviewerId && m.RoleInCouncil != CouncilMemberRole.Member)) return Fail("ContextInvalid");
        var inside = members.Any(m => m.UserId == r.ReviewerId);
        var required = members.Where(m => m.UserId != r.ReviewerId).ToArray();
        if (facts.Scores.Count != required.Length || facts.Scores.Select(s => s.CouncilMemberId).Distinct().Count() != required.Length
            || facts.Scores.Any(s => s.CouncilAssignmentId != assignment.Id || !required.Any(m => m.Id == s.CouncilMemberId)
                || s.Score is < 0 or > 10 || s.Version == 0 || s.EvaluatedAt == default)) return Fail("MissingRequiredSource");
        if (!IgnoredValid(a.IsIgnoredInCalculation, a.ExclusionReason) || !IgnoredValid(e.IsIgnoredInCalculation, e.ExclusionReason)
            || facts.Scores.Any(s => !IgnoredValid(s.IsIgnoredInCalculation, s.ExclusionReason))) return Fail("ContextInvalid");
        foreach (var s in facts.Scores)
        {
            var details = facts.Details.Where(d => d.EvaluationId == s.Id).ToArray();
            if (details.Length == 0 || details.Any(d => d.Score < 0 || d.CriteriaId == Guid.Empty) || details.Select(d => d.CriteriaId).Distinct().Count() != details.Length
                || Math.Round(details.Sum(d => d.Score), 2) != s.Score) return Fail("MissingRequiredSource");
        }
        return Result.Success(new ValidatedSources(a, r, e, assignment, council, minute, members, required, inside));
    }
    private static bool Id(Guid? id) => id.HasValue && id != Guid.Empty;
    private static bool Sha(string? sha) => sha is { Length: 64 } && sha.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool IgnoredValid(bool ignored, string? reason) => !ignored || (!string.IsNullOrWhiteSpace(reason) && reason == reason.Trim());
}
