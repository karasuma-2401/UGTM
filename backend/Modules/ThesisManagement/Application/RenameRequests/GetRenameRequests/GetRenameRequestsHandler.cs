using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.GetRenameRequests;

public sealed class GetRenameRequestsHandler(
    IRenameRequestRepository renameRequestRepository,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<RenameRequestDto>>> HandleAsync(
        GetRenameRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<IReadOnlyList<RenameRequestDto>>(RenameRequestErrors.AccessDenied);
        }

        var isPrivilegedStaff = ThesisAuthorization.IsAdministrator(currentUser)
            || ThesisAuthorization.CanReviewRenameRequestFaculty(currentUser)
            || ThesisAuthorization.CanReviewRenameRequestAcademicAffairs(currentUser);

        var isLecturerOrAdvisor = currentUser.Roles.Any(r =>
            string.Equals(r, "Lecturer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(r, "Advisor", StringComparison.OrdinalIgnoreCase));

        if (!isPrivilegedStaff && !isLecturerOrAdvisor)
        {
            return Result.Failure<IReadOnlyList<RenameRequestDto>>(RenameRequestErrors.AccessDenied);
        }

        RenameRequestStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<RenameRequestStatus>(query.Status, ignoreCase: true, out var s))
            {
                parsedStatus = s;
            }
            else
            {
                return Result.Failure<IReadOnlyList<RenameRequestDto>>(RenameRequestErrors.InvalidRequest);
            }
        }

        var requests = await renameRequestRepository.ListAsync(
            query.GroupId,
            parsedStatus,
            topicIdsFilter: null,
            cancellationToken);

        var dtos = requests.Select(RenameRequestMapper.ToDto).ToList();
        return Result.Success<IReadOnlyList<RenameRequestDto>>(dtos);
    }
}
