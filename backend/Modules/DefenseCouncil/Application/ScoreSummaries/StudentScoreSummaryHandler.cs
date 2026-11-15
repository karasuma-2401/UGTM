using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;

namespace UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

public sealed record StudentScoreComponentDto(string ComponentName, decimal Score, decimal Weight);
public sealed record StudentScoreSummaryDto(Guid GroupId, string TopicTitle, string Status, DateTime PublishedAt, decimal FinalScore,
    string CalculationFormula, IReadOnlyList<StudentScoreComponentDto> Components);

public sealed class StudentScoreSummaryHandler(ScoreSummaryAccess access, IReviewerFeedbackGroupReader groups, IScoreSummaryStore store)
{
    public async Task<Result<StudentScoreSummaryDto>> HandleAsync(Guid groupId, CancellationToken token = default)
    {
        if (access.AuthenticationError is { } auth) return Result.Failure<StudentScoreSummaryDto>(auth);
        if (groupId == Guid.Empty) return Result.Failure<StudentScoreSummaryDto>(ScoreSummaryErrors.InvalidRequest);
        if (!ScoreSummaryAccess.Role(await access.ActorAsync(false, token), "Student")) return Result.Failure<StudentScoreSummaryDto>(ScoreSummaryErrors.AccessDenied);
        var group = await groups.GetAsync(groupId, access.ActorId, token);
        if (group is null || !group.IsContextValid || !group.IsConfirmedMember) return Result.Failure<StudentScoreSummaryDto>(ScoreSummaryErrors.AccessDenied);
        var history = (await store.HistoriesAsync([groupId], token)).GetValueOrDefault(groupId);
        var summary = history?.Summary;
        if (summary is null || summary.IsLegacy || summary.Status != ScoreSummaryStatus.PublishedToStudent || summary.PublishedRevisionId is null || summary.PublishedAt is null)
            return Result.Failure<StudentScoreSummaryDto>(Error.Forbidden("ScoreSummary.NotYetPublished", "Điểm của nhóm chưa được công bố chính thức."));
        var revision = history!.Revisions.SingleOrDefault(r => r.Id == summary.PublishedRevisionId && r.GroupId == groupId && r.BatchId == group.BatchId);
        if (revision is null || !TryReadSnapshot(revision.SourceSnapshot, groupId, group.TopicId, revision.BatchId, out var title))
            return Result.Failure<StudentScoreSummaryDto>(Error.Forbidden("ScoreSummary.ArchivalNotConfirmed", "Nhóm chưa hoàn thành nộp lưu chiểu nên chưa thể tra cứu điểm chính thức."));
        var rawComponents = history.Components.Where(c => c.RevisionId == revision.Id && !c.IsIgnoredInCalculation && c.Weight > 0)
            .OrderBy(c => c.ComponentCode, StringComparer.Ordinal).ToArray();
        if (rawComponents.Length == 0) return Result.Failure<StudentScoreSummaryDto>(ScoreSummaryErrors.Conflict("ContextInvalid"));

        var facts = (await store.FactsAsync([groupId], false, token)).GetValueOrDefault(groupId);
        var studentId = access.ActorId;
        var hasPerStudentDetails = facts?.Details.Any(d => d.StudentId == studentId) == true;

        var components = rawComponents.Select(c =>
        {
            var resolvedScore = hasPerStudentDetails && facts is not null
                ? ResolveStudentComponentScore(c, facts, studentId) ?? c.Score
                : c.Score;
            return new StudentScoreComponentDto(c.ComponentName ?? c.ComponentCode, resolvedScore, c.Weight);
        }).ToArray();

        var finalScore = hasPerStudentDetails && revision.TotalWeight > 0
            ? Math.Round(components.Sum(c => c.Score * c.Weight) / revision.TotalWeight, 2, MidpointRounding.AwayFromZero)
            : revision.FinalScore;

        return Result.Success(new StudentScoreSummaryDto(groupId, title, "PublishedToStudent", summary.PublishedAt.Value, finalScore, revision.CalculationFormula, components));
    }

    private static decimal? ResolveStudentComponentScore(Domain.Entities.ScoreSummaryComponent component, ScoreFacts facts, Guid studentId)
    {
        if (string.IsNullOrWhiteSpace(component.SourceReferences)) return null;
        try
        {
            using var doc = JsonDocument.Parse(component.SourceReferences);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var sourceScores = new List<decimal>();
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                var idStr = elem.ValueKind == JsonValueKind.String ? elem.GetString() : null;
                if (Guid.TryParse(idStr, out var evalId))
                {
                    var studentDetails = facts.Details.Where(d => d.EvaluationId == evalId && d.StudentId == studentId).ToList();
                    if (studentDetails.Count > 0)
                    {
                        sourceScores.Add(Math.Round(studentDetails.Sum(d => d.Score), 2, MidpointRounding.AwayFromZero));
                    }
                }
            }
            return sourceScores.Count > 0
                ? Math.Round(sourceScores.Average(), 2, MidpointRounding.AwayFromZero)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadSnapshot(string snapshot, Guid group, Guid topic, Guid batch, out string title)
    {
        title = "";
        try
        {
            using var json = JsonDocument.Parse(snapshot); var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 13 || root[0].GetString() != "ThesisScoreInputs:v1"
                || root[1].GetString() != ScoreSummaryResolver.N(group) || root[2].GetString() != ScoreSummaryResolver.N(topic)
                || root[3].GetString() != ScoreSummaryResolver.N(batch) || root[5].ValueKind != JsonValueKind.String) return false;
            var proof = root[12];
            if (proof.ValueKind != JsonValueKind.Array || proof.GetArrayLength() != 12 || proof[0].ValueKind != JsonValueKind.String
                || proof[2].ValueKind != JsonValueKind.String || proof[5].ValueKind != JsonValueKind.String || proof[6].ValueKind != JsonValueKind.String
                || proof[9].ValueKind != JsonValueKind.String || proof[10].ValueKind != JsonValueKind.String
                || proof[11].ValueKind != JsonValueKind.Array || proof[11].GetArrayLength() != 2) return false;
            title = root[5].GetString()!; return true;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or IndexOutOfRangeException) { return false; }
    }
}
