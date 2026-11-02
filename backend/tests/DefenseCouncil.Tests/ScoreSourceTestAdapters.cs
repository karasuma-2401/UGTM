using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;

namespace UGTM.Modules.DefenseCouncil.Tests;

// Focused in-memory council tests; the production guard/authority is exercised in PG workflow tests.
internal sealed class TestCouncilScoreWriteGuard : ICouncilScoreWriteGuard
{
    public Task<Result<T>> ExecuteAsync<T>(Guid assignmentId, Func<CancellationToken, Task<Result<T>>> write, CancellationToken token) => write(token);
}
internal sealed class TestScoreAuthority(ICurrentUser user) : IFinalSubmissionAuthorityReader
{
    public Task<FinalSubmissionAuthorityDto?> GetAsync(Guid userId, CancellationToken token = default) => Task.FromResult<FinalSubmissionAuthorityDto?>(
        user.UserId == userId ? new(userId, user.IsAuthenticated, user.Roles.ToArray(), user.Permissions.ToArray()) : null);
    public Task<FinalSubmissionAuthorityDto?> LockAsync(Guid userId, CancellationToken token = default) => GetAsync(userId, token);
}
