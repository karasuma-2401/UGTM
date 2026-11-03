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
        var components = history.Components.Where(c => c.RevisionId == revision.Id && !c.IsIgnoredInCalculation && c.Weight > 0)
            .OrderBy(c => c.ComponentCode, StringComparer.Ordinal).Select(c => new StudentScoreComponentDto(c.ComponentName ?? c.ComponentCode, c.Score, c.Weight)).ToArray();
        if (components.Length == 0) return Result.Failure<StudentScoreSummaryDto>(ScoreSummaryErrors.Conflict("ContextInvalid"));
        return Result.Success(new StudentScoreSummaryDto(groupId, title, "PublishedToStudent", summary.PublishedAt.Value, revision.FinalScore, revision.CalculationFormula, components));
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
