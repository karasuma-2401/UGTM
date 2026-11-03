using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ScoreSummaries;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

public interface ICouncilScoreWriteGuard
{
    Task<Result<T>> ExecuteAsync<T>(Guid assignmentId, Func<CancellationToken, Task<Result<T>>> write, CancellationToken token);
}
public sealed class CouncilScoreWriteGuard(IScoreSummaryWorkflowTransactionCoordinator coordinator, IScoreSummaryStore store,
    IReviewerAssignmentThesisReader groups, IFinalSubmissionAuthorityReader authority, ICurrentUser user) : ICouncilScoreWriteGuard
{
    public async Task<Result<T>> ExecuteAsync<T>(Guid assignmentId, Func<CancellationToken, Task<Result<T>>> write, CancellationToken token)
    {
        if (!user.IsAuthenticated || user.UserId is not { } actor) return Result.Failure<T>(CouncilErrors.Forbidden);
        var account = await authority.GetAsync(actor, token);
        if (!ScoreSummaryAccess.Role(account, "Lecturer") && !ScoreSummaryAccess.Role(account, "Admin")) return Result.Failure<T>(CouncilErrors.Forbidden);
        var group = await store.AssignmentGroupAsync(assignmentId, token);
        if (group is null) return Result.Failure<T>(CouncilErrors.AssignmentNotFound);
        try
        {
            return await coordinator.ExecuteWithPostSaveAsync(async ct =>
            {
                if (await groups.LockAsync(group.Value, ct) is null) return ScoreSummaryTransactionResult<Result<T>>.Abort(Result.Failure<T>(CouncilErrors.AssignmentNotFound));
                account = await authority.LockAsync(actor, ct);
                if (!ScoreSummaryAccess.Role(account, "Lecturer") && !ScoreSummaryAccess.Role(account, "Admin")) return ScoreSummaryTransactionResult<Result<T>>.Abort(Result.Failure<T>(CouncilErrors.Forbidden));
                // Group root is held before council/score row locks and retained through the write.
                await store.FactsAsync([group.Value], true, ct);
                if (await store.PublishedAsync([group.Value], ct)) return ScoreSummaryTransactionResult<Result<T>>.Abort(Result.Failure<T>(CouncilErrors.ScoresAlreadyPublished));
                var result = await write(ct);
                return result.IsFailure ? ScoreSummaryTransactionResult<Result<T>>.Abort(result) : ScoreSummaryTransactionResult<Result<T>>.Commit(result);
            }, (result, _) => Task.FromResult(result), token);
        }
        catch (DefenseCouncilPersistenceException) { return Result.Failure<T>(ScoreSummaryErrors.Conflict("ConcurrencyConflict")); }
    }
}
