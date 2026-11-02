using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.ThesisManagement.Application.FinalSubmissions;

public sealed class FinalSubmissionReadHandler(IFinalSubmissionStore store, FinalSubmissionAccess access,
    ILockedDefenseProofReader defense, IReviewerAssignmentTopicReader topics, IThesisSubmissionObjectStorage storage, TimeProvider clock)
{
    public async Task<Result<FinalSubmissionDto>> OwnAsync(Guid groupId, CancellationToken token)
    {
        var context = await access.ContextAsync(groupId, true, false, token);
        if (context.IsFailure) return Result.Failure<FinalSubmissionDto>(context.Error);
        var row = await store.GetByGroupAsync(groupId, false, token);
        if (row is null || row.Status == FinalSubmissionStatus.NotSubmitted) return Result.Failure<FinalSubmissionDto>(FinalSubmissionErrors.NotFound);
        return Result.Success(FinalSubmissionMapping.Dto(row, await store.GetPackagesAsync(row.Id, token)));
    }
    public async Task<Result<FinalSubmissionDto>> DetailAsync(Guid id, CancellationToken token)
    {
        var row = await AuthorizedAsync(id, token);
        return row.IsFailure ? Result.Failure<FinalSubmissionDto>(row.Error)
            : Result.Success(FinalSubmissionMapping.Dto(row.Value, await store.GetPackagesAsync(id, token)));
    }
    public async Task<Result<IReadOnlyList<FinalSubmissionPackageDto>>> PackagesAsync(Guid id, CancellationToken token)
    {
        var row = await AuthorizedAsync(id, token);
        return row.IsFailure ? Result.Failure<IReadOnlyList<FinalSubmissionPackageDto>>(row.Error)
            : Result.Success(FinalSubmissionMapping.Packages(await store.GetPackagesAsync(id, token)));
    }
    public async Task<Result<FinalSubmissionDownloadDto>> DownloadAsync(Guid id, int version, string role, CancellationToken token)
    {
        if (version < 1 || role is not ("ThesisPdf" or "ThesisDocx")) return Result.Failure<FinalSubmissionDownloadDto>(FinalSubmissionErrors.InvalidRequest);
        var row = await AuthorizedAsync(id, token);
        if (row.IsFailure) return Result.Failure<FinalSubmissionDownloadDto>(row.Error);
        var graph = await store.GetPackagesAsync(id, token);
        var package = graph.Packages.SingleOrDefault(p => p.VersionNo == version);
        var link = graph.Links.SingleOrDefault(l => l.PackageId == package?.Id && l.FileRole == role);
        var file = graph.Files.SingleOrDefault(f => f.Id == link?.SubmissionFileId && f.EntityType == SubmissionEntityType.FinalSubmission
            && f.EntityId == id && f.VersionNo == version);
        if (file is null) return Result.Failure<FinalSubmissionDownloadDto>(FinalSubmissionErrors.NotFound);
        // Authorization is checked for this request; no URL is cached in a package or receipt.
        var uri = await storage.CreateDownloadUriAsync(file.ObjectKey, file.FileName, TimeSpan.FromSeconds(300), token);
        return Result.Success(new FinalSubmissionDownloadDto(file.Id, file.FileName, role, file.FileSize, uri.AbsoluteUri, clock.GetUtcNow().UtcDateTime.AddSeconds(300)));
    }
    public async Task<Result<FinalSubmissionEligibilityDto>> EligibilityAsync(Guid groupId, CancellationToken token)
    {
        var context = await access.ContextAsync(groupId, true, false, token);
        if (context.IsFailure) return Result.Failure<FinalSubmissionEligibilityDto>(context.Error);
        var row = await store.GetByGroupAsync(groupId, false, token);
        var proof = await defense.GetAsync(groupId, token);
        var batch = context.Value.Batch;
        var now = clock.GetUtcNow().UtcDateTime;
        var open = batch?.BatchStatus == "Open";
        var window = batch?.WindowOpensAtUtc is { } start && batch.WindowDeadlineAtUtc is { } end && now >= start && now <= end;
        var completed = proof.Outcome == LockedDefenseOutcome.Succeeded && proof.BatchId == context.Value.Topic.BatchId;
        List<string> reasons = [];
        if (!open) reasons.Add(FinalSubmissionErrors.BatchNotWritable.Code);
        if (!window) reasons.Add(FinalSubmissionErrors.WindowClosed.Code);
        if (!completed) reasons.Add(FinalSubmissionErrors.DefenseNotLocked.Code);
        if (row?.Status is FinalSubmissionStatus.AdvisorConfirmed or FinalSubmissionStatus.ArchiveConfirmed) reasons.Add(FinalSubmissionErrors.Locked.Code);
        return Result.Success(new FinalSubmissionEligibilityDto(groupId, reasons.Count == 0, open, window, batch?.WindowDeadlineAtUtc,
            completed, completed ? "Locked" : proof.Outcome.ToString(), row?.Status.ToString() ?? "NotSubmitted", row?.Version, reasons));
    }
    public async Task<Result<FinalSubmissionPageDto>> ListAsync(Guid? batchId, string? status, string? search, int page, int size, CancellationToken token)
    {
        if (access.AuthenticationError is { } error) return Result.Failure<FinalSubmissionPageDto>(error);
        var actor = await access.ActorAsync(false, token);
        var manager = FinalSubmissionAccess.IsManager(actor);
        if (!manager && !FinalSubmissionAccess.HasRole(actor, "Lecturer")) return Result.Failure<FinalSubmissionPageDto>(FinalSubmissionErrors.AccessDenied);
        if (batchId == Guid.Empty || page < 1 || size is < 1 or > 100 || search?.Length > 200
            || (status is not null && (!Enum.TryParse<FinalSubmissionStatus>(status, false, out var parsed) || !Enum.IsDefined(parsed) || parsed.ToString() != status)))
            return Result.Failure<FinalSubmissionPageDto>(FinalSubmissionErrors.InvalidRequest);
        Guid? groupSelector = Guid.TryParse(search?.Trim(), out var searchedId) ? searchedId : null;
        var topicIds = groupSelector.HasValue
            ? (await store.GetGroupTopicsAsync([groupSelector.Value], token)).Values.Distinct().ToArray()
            : await topics.FindIdsAsync(batchId, search, token);
        var topicRows = await topics.GetManyAsync(topicIds, token);
        if (batchId.HasValue) topicRows = topicRows.Where(t => t.BatchId == batchId).ToArray();
        if (!manager) topicRows = topicRows.Where(t => t.AdvisorId == access.ActorId).ToArray();
        var byId = topicRows.ToDictionary(t => t.TopicId);
        var (rows, count) = await store.ListAsync(byId.Keys.ToArray(), groupSelector, status, page, size, token);
        // Batch/topic projections are loaded once, not one owner call per row.
        // Group identity lookup is batched by the persistence reader below.
        var summaries = new List<FinalSubmissionSummaryDto>();
        var groups = await store.GetGroupTopicsAsync(rows.Select(r => r.GroupId).ToArray(), token);
        foreach (var row in rows)
        {
            var topic = byId[groups[row.GroupId]];
            summaries.Add(new(row.Id, row.GroupId, topic.BatchId, topic.TitleVi, row.Status.ToString(), row.CurrentVersionNo,
                row.Version, row.SubmittedAtUtc, row.AdvisorConfirmedAt, row.ArchivedConfirmedAt));
        }
        return Result.Success(new FinalSubmissionPageDto(summaries, count, page, size));
    }
    private async Task<Result<FinalSubmission>> AuthorizedAsync(Guid id, CancellationToken token)
    {
        if (access.AuthenticationError is { } error) return Result.Failure<FinalSubmission>(error);
        if (id == Guid.Empty) return Result.Failure<FinalSubmission>(FinalSubmissionErrors.InvalidRequest);
        var actor = await access.ActorAsync(false, token);
        if (actor is not { IsActive: true } || !(FinalSubmissionAccess.IsManager(actor)
            || FinalSubmissionAccess.HasRole(actor, "Student") || FinalSubmissionAccess.HasRole(actor, "Lecturer")))
            return Result.Failure<FinalSubmission>(FinalSubmissionErrors.AccessDenied);
        var row = await store.GetByIdAsync(id, token);
        if (row is null) return Result.Failure<FinalSubmission>(FinalSubmissionErrors.NotFound);
        var context = await access.ContextAsync(row.GroupId, false, false, token);
        return context.IsFailure ? Result.Failure<FinalSubmission>(context.Error) : Result.Success(row);
    }
}
