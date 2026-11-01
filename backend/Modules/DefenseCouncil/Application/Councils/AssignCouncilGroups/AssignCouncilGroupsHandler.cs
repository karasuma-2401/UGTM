using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;

public sealed record AssignCouncilGroupsCommand(
    Guid CouncilId,
    IReadOnlyList<CouncilAssignmentInputDto> Assignments);

public sealed class AssignCouncilGroupsHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    IDefenseGroupContract defenseGroupContract,
    ITopicDefenseContract topicDefenseContract,
    ICouncilUserInfoReader userInfoReader,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<CouncilAssignmentResponseDto>>> HandleAsync(
        AssignCouncilGroupsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(CouncilErrors.Forbidden);
        }

        var isStaffOrAdmin = currentUser.Roles.Any(r => r is "Admin" or "AcademicAffairs" or "Faculty");
        if (!isStaffOrAdmin)
        {
            return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(CouncilErrors.Forbidden);
        }

        if (command.Assignments.Count == 0)
        {
            return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(
                Error.Validation("Council.EmptyAssignments", "Danh sách nhóm đề tài không được để trống."));
        }

        var council = await councilRepository.GetByIdWithDetailsAsync(command.CouncilId, cancellationToken);
        if (council is null)
        {
            return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(CouncilErrors.NotFound);
        }

        if (council.Status != CouncilStatus.Draft)
        {
            return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(CouncilErrors.NotInDraftStatus);
        }

        // Validate duplicates within the request
        if (command.Assignments.Select(a => a.GroupId).Distinct().Count() != command.Assignments.Count)
        {
            return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(
                Error.Conflict("Council.DuplicateGroupInRequest", "Danh sách nhóm đề tài gửi lên có nhóm bị lặp lại."));
        }

        if (command.Assignments.Any(a => a.OrderNumber < 1))
        {
            return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(
                Error.Validation("Council.InvalidOrderNumber", "Thứ tự bảo vệ (OrderNumber) phải lớn hơn hoặc bằng 1."));
        }

        // Validate order number conflicts with existing assignments or duplicates in request
        var existingOrderNumbers = council.Assignments.Select(a => a.OrderNumber).ToHashSet();
        var requestOrderNumbers = new HashSet<int>();
        foreach (var assignment in command.Assignments)
        {
            if (existingOrderNumbers.Contains(assignment.OrderNumber) || !requestOrderNumbers.Add(assignment.OrderNumber))
            {
                return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(CouncilErrors.DuplicateOrderNumber);
            }
        }

        var groupIds = command.Assignments.Select(a => a.GroupId).Distinct().ToList();

        // Check if any group is already assigned anywhere across councils
        var assignedCouncils = await councilRepository.GetAssignedGroupCouncilsAsync(groupIds, null, cancellationToken);
        if (assignedCouncils.Count > 0)
        {
            var firstAssignedGroupId = assignedCouncils.Keys.First();
            return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(
                CouncilErrors.GroupAlreadyAssigned(firstAssignedGroupId));
        }

        // Check groups in ThesisManagement
        var groups = await defenseGroupContract.GetGroupsByIdsAsync(groupIds, cancellationToken);
        foreach (var groupId in groupIds)
        {
            if (!groups.TryGetValue(groupId, out var groupSnapshot))
            {
                return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(CouncilErrors.GroupNotFound(groupId));
            }

            if (groupSnapshot.BatchId != Guid.Empty && groupSnapshot.BatchId != council.BatchId)
            {
                return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(CouncilErrors.GroupNotInBatch(groupId));
            }

            if (string.Equals(groupSnapshot.Status, "Stopped", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(CouncilErrors.GroupStopped(groupId));
            }
        }

        // Check if any group was rejected by reviewer
        var rejectedGroupIds = await councilRepository.GetRejectedDefenseGroupIdsAsync(groupIds, cancellationToken);
        if (rejectedGroupIds.Count > 0)
        {
            return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(
                CouncilErrors.GroupDefenseRejectedByReviewer(rejectedGroupIds.First()));
        }

        // Check Conflict of Interest: Advisor cannot be a member in this council
        var topicIds = groups.Values.Select(g => g.TopicId).Distinct().ToList();
        var topics = await topicDefenseContract.GetTopicsDetailsAsync(topicIds, cancellationToken);

        var councilMemberUserIds = council.Members.Select(m => m.UserId).ToHashSet();
        if (councilMemberUserIds.Count > 0)
        {
            foreach (var topic in topics.Values)
            {
                if (councilMemberUserIds.Contains(topic.AdvisorId))
                {
                    var advisorUser = await userInfoReader.GetUserByIdAsync(topic.AdvisorId, cancellationToken);
                    var advisorName = advisorUser?.FullName ?? topic.AdvisorId.ToString();
                    return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(
                        CouncilErrors.AdvisorIsInCouncil(advisorName, topic.TitleVi));
                }
            }
        }

        // Add assignments
        var newAssignments = command.Assignments.Select(a => new CouncilAssignment
        {
            Id = Guid.NewGuid(),
            CouncilId = council.Id,
            GroupId = a.GroupId,
            OrderNumber = a.OrderNumber
        }).ToList();

        await councilRepository.AddAssignmentsAsync(newAssignments, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var resultDtos = newAssignments.Select(a =>
        {
            var topic = groups.TryGetValue(a.GroupId, out var g) && topics.TryGetValue(g.TopicId, out var t)
                ? t
                : null;

            return new CouncilAssignmentResponseDto(
                a.Id,
                a.CouncilId,
                a.GroupId,
                a.OrderNumber,
                topic?.TitleVi,
                topic?.TitleEn);
        }).ToList();

        return Result.Success<IReadOnlyList<CouncilAssignmentResponseDto>>(resultDtos);
    }
}
