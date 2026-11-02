using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.FinalSubmissions;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

public sealed record ScoreContext(ReviewerAssignmentThesisContractDto Group, ReviewerAssignmentTopicContractDto Topic, FinalSubmissionBatchContractDto? Batch);
public sealed class ScoreSummaryAccess(ICurrentUser user, IFinalSubmissionAuthorityReader authority, IReviewerAssignmentThesisReader groups,
    IReviewerAssignmentTopicReader topics, IFinalSubmissionBatchReader batches, IScoreSummaryStore store)
{
    public const string Read = "councils.score-summaries.read";
    public const string Manage = "councils.score-summaries.manage";
    public const string Publish = "councils.score-summaries.publish";
    public Guid ActorId => user.UserId ?? Guid.Empty;
    public Error? AuthenticationError => !user.IsAuthenticated || ActorId == Guid.Empty ? ScoreSummaryErrors.AuthenticationRequired : null;
    public static bool Role(FinalSubmissionAuthorityDto? actor, string role) => actor is { IsActive: true } && actor.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public static bool Allowed(FinalSubmissionAuthorityDto? actor, string permission) => Role(actor, "Admin")
        || ((Role(actor, "Faculty") || (permission == Read && Role(actor, "AcademicAffairs"))) && actor!.Permissions.Contains(permission));
    public Task<FinalSubmissionAuthorityDto?> ActorAsync(bool locked, CancellationToken token) => AuthenticationError is not null ? Task.FromResult<FinalSubmissionAuthorityDto?>(null) : locked ? authority.LockAsync(ActorId, token) : authority.GetAsync(ActorId, token);
    public async Task<Error?> AuthorizeAsync(string permission, bool locked, CancellationToken token)
        => AuthenticationError ?? (Allowed(await ActorAsync(locked, token), permission) ? null : ScoreSummaryErrors.AccessDenied);
    public async Task<Result<ScoreContext>> ContextAsync(Guid group, bool locked, CancellationToken token)
    {
        var g = locked ? await groups.LockAsync(group, token) : await groups.GetAsync(group, token);
        if (g is null) return Result.Failure<ScoreContext>(ScoreSummaryErrors.NotFound);
        var t = locked ? await topics.LockAsync(g.TopicId, token) : await topics.GetAsync(g.TopicId, token);
        if (t is null || g.BatchId != t.BatchId) return Result.Failure<ScoreContext>(ScoreSummaryErrors.Conflict("ContextInvalid"));
        var b = locked ? await batches.LockAsync(t.BatchId, token) : await batches.GetAsync(t.BatchId, token);
        if (locked) await store.LockAdvisorsAsync([group], token);
        return Result.Success(new ScoreContext(g, t, b));
    }
}
