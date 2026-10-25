using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;

namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations.AdvisorUnassignTopicStudents;

public sealed class AdvisorUnassignTopicStudentsHandler(
    ITopicRegistrationStore store,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result> HandleAsync(
        AdvisorUnassignTopicStudentsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure(
                Error.Unauthorized("Auth.Unauthorized", "Vui lòng đăng nhập để thực hiện thao tác."));
        }

        var isAdmin = HasRole(currentUser, "Admin");
        var isLecturer = HasRole(currentUser, "Lecturer");
        if (!isAdmin && !isLecturer)
        {
            return Result.Failure(TopicRegistrationErrors.OnlyAdvisorsAllowed);
        }

        if (command.TopicId == Guid.Empty)
        {
            return Result.Failure(TopicRegistrationErrors.TopicNotFound);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        return await store.UnassignTopicStudentsByAdvisorAsync(
            command.TopicId,
            currentUser.UserId.Value,
            isAdmin,
            nowUtc,
            cancellationToken);
    }

    private static bool HasRole(ICurrentUser user, string role)
        => user.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}
