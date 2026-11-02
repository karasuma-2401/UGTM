using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.FinalSubmissions;
using UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

public sealed record LockedScoreInput(ScoreContext Context, FinalSubmissionArchiveProofContractDto Archive, ScoreFacts Facts,
    ScoreSummaryConfigurationDto? Config, ScoreSummary? Summary);
public sealed record LockedScoreInputs(IReadOnlyDictionary<Guid, LockedScoreInput> Inputs, IReadOnlyDictionary<Guid, Error> Errors);

// One lock pipeline for single calculation and selected publication. Every phase finishes for
// all groups before advancing; owner readers in later phases never reacquire earlier roots.
public sealed class ScoreSummaryInputLoader(ScoreSummaryAccess access, IScoreSummaryStore store, IReviewerAssignmentThesisReader groups,
    IReviewerAssignmentTopicReader topics, IFinalSubmissionBatchReader batches, IFinalSubmissionArchiveProofContract archives, IScoreSummaryConfigurationReader configs)
{
    public async Task<Result<LockedScoreInputs>> LockManyAsync(IReadOnlyCollection<Guid> groupIds, string permission, CancellationToken token)
    {
        if (await access.AuthorizeAsync(permission, false, token) is { } denied) return Result.Failure<LockedScoreInputs>(denied);
        if (groupIds.Count is < 1 or > 100 || groupIds.Contains(Guid.Empty) || groupIds.Distinct().Count() != groupIds.Count)
            return Result.Failure<LockedScoreInputs>(ScoreSummaryErrors.InvalidRequest);
        var ids = groupIds.Order().ToArray();
        var roots = new Dictionary<Guid, ReviewerAssignmentThesisContractDto>(); var errors = new Dictionary<Guid, Error>();
        foreach (var id in ids)
        {
            var g = await groups.LockAsync(id, token);
            if (g is null) errors[id] = ScoreSummaryErrors.NotFound; else roots[id] = g;
        }
        var tt = new Dictionary<Guid, ReviewerAssignmentTopicContractDto>();
        foreach (var id in roots.Values.Select(g => g.TopicId).Distinct().Order())
            if (await topics.LockAsync(id, token) is { } t) tt[id] = t;
        var bb = new Dictionary<Guid, FinalSubmissionBatchContractDto>();
        foreach (var id in tt.Values.Select(t => t.BatchId).Distinct().Order())
            if (await batches.LockAsync(id, token) is { } b) bb[id] = b;
        await store.LockAdvisorsAsync(ids, token);
        if (await access.AuthorizeAsync(permission, true, token) is { } lost) return Result.Failure<LockedScoreInputs>(lost);
        var contexts = new Dictionary<Guid, ScoreContext>();
        foreach (var g in roots.Values)
        {
            if (!tt.TryGetValue(g.TopicId, out var t) || g.BatchId != t.BatchId) errors[g.GroupId] = ScoreSummaryErrors.Conflict("ContextInvalid");
            else if (!bb.TryGetValue(t.BatchId, out var b) || b.BatchStatus is not ("Open" or "Closed")) errors[g.GroupId] = ScoreSummaryErrors.Conflict("BatchNotWritable");
            else contexts[g.GroupId] = new(g, t, b);
        }
        var proofs = new Dictionary<Guid, FinalSubmissionArchiveProofContractDto>();
        foreach (var id in contexts.Keys.Order()) proofs[id] = await archives.LockArchiveProofForCalculationAsync(id, token);
        var facts = await store.FactsAsync(contexts.Keys.ToArray(), true, token);
        var configurations = new Dictionary<Guid, ScoreSummaryConfigurationDto?>();
        foreach (var batch in contexts.Values.Select(c => c.Topic.BatchId).Distinct().Order()) configurations[batch] = await configs.LockAsync(batch, token);
        var inputs = new Dictionary<Guid, LockedScoreInput>();
        foreach (var id in contexts.Keys.Order()) inputs[id] = new(contexts[id], proofs[id], facts[id], configurations[contexts[id].Topic.BatchId], await store.SummaryAsync(id, true, token));
        return Result.Success(new LockedScoreInputs(inputs, errors));
    }
}
