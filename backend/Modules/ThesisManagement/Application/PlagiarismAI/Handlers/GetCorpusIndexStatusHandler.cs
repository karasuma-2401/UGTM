using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;

public sealed class GetCorpusIndexStatusHandler(IPlagiarismAiClient aiClient, ICurrentUser currentUser)
{
    public async Task<Result<PlagiarismJobStatusDto>> HandleAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var faculty = currentUser.Roles.Any(r => string.Equals(r, "Faculty", StringComparison.OrdinalIgnoreCase))
            && ThesisAuthorization.CanReviewGates(currentUser);
        if (!ThesisAuthorization.IsAdministrator(currentUser) && !faculty)
            return Result.Failure<PlagiarismJobStatusDto>(PlagiarismScanErrors.AccessDenied);
        if (jobId == Guid.Empty)
            return Result.Failure<PlagiarismJobStatusDto>(PlagiarismScanErrors.NotFound);
        var job = await aiClient.GetJobStatusAsync(jobId, cancellationToken);
        if (job is null || !string.Equals(job.Type, "index", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<PlagiarismJobStatusDto>(PlagiarismScanErrors.NotFound);
        return Result.Success(job);
    }
}
