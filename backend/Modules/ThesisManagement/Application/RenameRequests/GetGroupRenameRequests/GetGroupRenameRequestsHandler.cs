using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.GetGroupRenameRequests;

public sealed class GetGroupRenameRequestsHandler(
    IRenameRequestRepository renameRequestRepository,
    IStudentGroupRepository groupRepository,
    ITopicRenameContract topicRenameContract,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<RenameRequestDto>>> HandleAsync(
        GetGroupRenameRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<IReadOnlyList<RenameRequestDto>>(RenameRequestErrors.AccessDenied);
        }

        if (query.GroupId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<RenameRequestDto>>(RenameRequestErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(query.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<IReadOnlyList<RenameRequestDto>>(RenameRequestErrors.GroupNotFound);
        }

        var isMember = group.Members.Any(m => m.StudentId == currentUser.UserId.Value && m.ConfirmationStatus == MemberConfirmationStatus.Confirmed);
        var isPrivilegedStaff = ThesisAuthorization.IsAdministrator(currentUser)
            || ThesisAuthorization.CanReviewRenameRequestFaculty(currentUser)
            || ThesisAuthorization.CanReviewRenameRequestAcademicAffairs(currentUser);

        var isAdvisor = false;
        if (!isMember && !isPrivilegedStaff)
        {
            var topicSnapshot = await topicRenameContract.GetTopicSnapshotAsync(group.TopicId, cancellationToken);
            if (topicSnapshot is not null && topicSnapshot.AdvisorId == currentUser.UserId.Value)
            {
                isAdvisor = true;
            }
        }

        if (!isMember && !isPrivilegedStaff && !isAdvisor)
        {
            return Result.Failure<IReadOnlyList<RenameRequestDto>>(RenameRequestErrors.AccessDenied);
        }

        var requests = await renameRequestRepository.GetByGroupIdAsync(query.GroupId, cancellationToken);
        var dtos = requests.Select(RenameRequestMapper.ToDto).ToList();
        return Result.Success<IReadOnlyList<RenameRequestDto>>(dtos);
    }
}
