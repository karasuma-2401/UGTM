using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.FinalSubmissions;
using UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

public sealed class ReadScoreSummaryHandler(IScoreSummaryStore store, ScoreSummaryAccess access, ScoreSummaryResolver resolver,
    IReviewerAssignmentThesisReader groups, IReviewerAssignmentTopicReader topics, IFinalSubmissionBatchReader batches,
    IFinalSubmissionArchiveProofContract archives, IScoreSummaryConfigurationReader configs, TimeProvider clock)
{
    public Task<Error?> AuthorizeAsync(CancellationToken token) => access.AuthorizeAsync(ScoreSummaryAccess.Read, false, token);
    private sealed record Row(ScoreSummaryDto? Detail, ScoreReadinessDto Readiness, ScoreListItemDto Item);
    public async Task<Result<ScoreSummaryDto>> DetailAsync(Guid group, CancellationToken token = default)
    {
        if (await AuthorizeAsync(token) is { } denied) return Result.Failure<ScoreSummaryDto>(denied);
        var context = await access.ContextAsync(group, false, token);
        if (context.IsFailure) return Result.Failure<ScoreSummaryDto>(context.Error);
        var rows = await ReadAsync([context.Value.Group], [context.Value.Topic], context.Value.Batch, token);
        return rows[0].Detail is { } dto ? Result.Success(dto) : Result.Failure<ScoreSummaryDto>(ScoreSummaryErrors.NotFound);
    }
    public async Task<Result<ScoreReadinessDto>> ReadinessAsync(Guid group, CancellationToken token = default)
    {
        if (await AuthorizeAsync(token) is { } denied) return Result.Failure<ScoreReadinessDto>(denied);
        var context = await access.ContextAsync(group, false, token);
        if (context.IsFailure) return Result.Failure<ScoreReadinessDto>(context.Error);
        return Result.Success((await ReadAsync([context.Value.Group], [context.Value.Topic], context.Value.Batch, token))[0].Readiness);
    }
    public async Task<Result<ScorePageDto>> ListAsync(Guid batch, string? status, bool? stale, bool? ready, int page, int size, CancellationToken token = default)
    {
        if (await AuthorizeAsync(token) is { } denied) return Result.Failure<ScorePageDto>(denied);
        if (batch == Guid.Empty || page < 1 || size is < 1 or > 100 || (status is not null && !Enum.GetNames<ScoreSummaryStatus>().Contains(status, StringComparer.Ordinal))) return Result.Failure<ScorePageDto>(ScoreSummaryErrors.InvalidRequest);
        var topicIds = await topics.FindIdsAsync(batch, null, token);
        var gg = await groups.GetByTopicIdsAsync(topicIds, token);
        var tt = await topics.GetManyAsync(gg.Select(g => g.TopicId).Distinct().ToArray(), token);
        var b = await batches.GetAsync(batch, token);
        var rows = (await ReadAsync(gg, tt, b, token)).Select(r => r.Item)
            .Where(r => (status is null || r.Status == status) && (!stale.HasValue || r.IsStale == stale) && (!ready.HasValue || r.IsReadyForPublication == ready)).OrderBy(r => r.GroupId).ToArray();
        var offset = ((long)page - 1) * size;
        return Result.Success(new ScorePageDto(offset >= rows.Length ? [] : rows.Skip((int)offset).Take(size).ToArray(), rows.Length, page, size));
    }
    private async Task<IReadOnlyList<Row>> ReadAsync(IReadOnlyList<ReviewerAssignmentThesisContractDto> gg, IReadOnlyList<ReviewerAssignmentTopicContractDto> tt,
        FinalSubmissionBatchContractDto? batch, CancellationToken token)
    {
        if (gg.Count == 0) return [];
        var ids = gg.Select(g => g.GroupId).ToArray(); var facts = await store.FactsAsync(ids, false, token);
        var history = await store.HistoriesAsync(ids, token);
        var proofs = (await archives.GetManyArchiveProofsAsync(ids, token)).ToDictionary(p => p.GroupId);
        var config = batch is null ? null : await configs.GetAsync(batch.BatchId, token);
        var topicMap = tt.ToDictionary(t => t.TopicId);
        return gg.Select(g =>
        {
            var h = history.GetValueOrDefault(g.GroupId); var f = facts[g.GroupId]; var t = topicMap.GetValueOrDefault(g.TopicId); var archive = proofs[g.GroupId];
            var resolved = t is null ? Result.Failure<ResolvedScoreCalculation>(ScoreSummaryErrors.Conflict("ContextInvalid")) : resolver.Resolve(g, t, f, archive, config, clock.GetUtcNow().UtcDateTime);
            var published = h?.Summary.Status == ScoreSummaryStatus.PublishedToStudent;
            var stale = !published && h?.Summary.Status == ScoreSummaryStatus.Calculated && (h.Summary.IsLegacy || resolved.IsFailure || resolved.Value.Fingerprint != h.Summary.InputFingerprint);
            var reasons = new List<string>();
            if (published) reasons.Add("ScoreSummary.AlreadyPublished");
            else
            {
                if (batch?.BatchStatus is not ("Open" or "Closed")) reasons.Add("ScoreSummary.BatchNotWritable");
                if (resolved.IsFailure) reasons.Add(resolved.Error.Code);
            }
            var canCalculate = reasons.Count == 0;
            if (stale) reasons.Add("ScoreSummary.Stale");
            if (h?.Summary.IsLegacy == true) reasons.Add("ScoreSummary.Legacy");
            var canPublish = canCalculate && h is { Summary.IsLegacy: false } && h.Summary.Status == ScoreSummaryStatus.Calculated && !stale;
            var readiness = new ScoreReadinessDto(g.GroupId, canCalculate, canPublish, f.Minutes.Count == 1 && f.Minutes[0].LockStatus == CouncilMinuteLockStatus.Locked && f.Minutes[0].LockedBy is { } actor && actor != Guid.Empty && f.Minutes[0].LockedAt.HasValue,
                t is not null && resolver.ValidateSources(g, t, f, false).IsSuccess,
                archive.Outcome == ArchiveProofOutcome.Succeeded, config is { IsValid: true }, h?.Summary.Version, reasons);
            var detail = h is null ? null : ScoreSummaryMapping.Dto(h, t?.BatchId ?? g.BatchId ?? Guid.Empty, stale);
            return new Row(detail, readiness, new ScoreListItemDto(g.GroupId, t?.TitleVi ?? "", h?.Summary.Id, h?.Summary.Version, h?.Summary.Status.ToString() ?? "Pending", h?.Summary.FinalScore, h?.Summary.IsLegacy ?? false,
                stale, canCalculate, canPublish, h?.Summary.CalculatedAt, h?.Summary.PublishedAt));
        }).ToArray();
    }
}
