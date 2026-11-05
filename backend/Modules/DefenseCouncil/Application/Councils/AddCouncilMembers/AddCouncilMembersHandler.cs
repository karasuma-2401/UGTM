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

namespace UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;

public sealed record AddCouncilMembersCommand(
    Guid CouncilId,
    IReadOnlyList<CouncilMemberInputDto> Members);

public sealed class AddCouncilMembersHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    ICouncilUserInfoReader userInfoReader,
    IDefenseGroupContract defenseGroupContract,
    ITopicDefenseContract topicDefenseContract,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<CouncilMemberResponseDto>>> HandleAsync(
        AddCouncilMembersCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.Forbidden);
        }

        var isStaffOrAdmin = currentUser.Roles.Any(r => r is "Admin" or "AcademicAffairs" or "Faculty");
        if (!isStaffOrAdmin)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.Forbidden);
        }

        if (command.Members.Count == 0)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(
                Error.Validation("Council.EmptyMembers", "Danh sách thành viên không được để trống."));
        }

        var council = await councilRepository.GetByIdWithDetailsAsync(command.CouncilId, cancellationToken);
        if (council is null)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.NotFound);
        }

        if (council.Status != CouncilStatus.Draft)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.NotInDraftStatus);
        }

        // Validate duplicates within the request
        if (command.Members.Select(m => m.UserId).Distinct().Count() != command.Members.Count)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(
                Error.Conflict("Council.DuplicateMemberInRequest", "Danh sách thành viên gửi lên có giảng viên bị lặp lại."));
        }

        var existingUserIds = council.Members.Select(m => m.UserId).ToHashSet();
        var existingChairmanCount = council.Members.Count(m => m.RoleInCouncil == CouncilMemberRole.Chairman);
        var existingSecretaryCount = council.Members.Count(m => m.RoleInCouncil == CouncilMemberRole.Secretary);

        var newChairmanCount = command.Members.Count(m => m.Role == CouncilMemberRole.Chairman);
        var newSecretaryCount = command.Members.Count(m => m.Role == CouncilMemberRole.Secretary);

        if (existingChairmanCount + newChairmanCount > 1)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.DuplicateChairman);
        }

        if (existingSecretaryCount + newSecretaryCount > 1)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.DuplicateSecretary);
        }

        var memberUserIds = command.Members.Select(m => m.UserId).Distinct().ToList();

        // Check if any member already exists in council
        var duplicateExistingUser = memberUserIds.FirstOrDefault(id => existingUserIds.Contains(id));
        if (duplicateExistingUser != Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(
                CouncilErrors.DuplicateMember(duplicateExistingUser.ToString()));
        }

        // Verify users exist and are active
        var usersInfo = await userInfoReader.GetUsersByIdsAsync(memberUserIds, cancellationToken);
        foreach (var userId in memberUserIds)
        {
            if (!usersInfo.TryGetValue(userId, out var user) || !user.IsActive)
            {
                return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(CouncilErrors.MemberNotFound(userId));
            }
        }

        // 1. Conflict of Interest validation: check against currently assigned groups
        if (council.Assignments.Count > 0)
        {
            var assignedGroupIds = council.Assignments.Select(a => a.GroupId).ToList();
            var groups = await defenseGroupContract.GetGroupsByIdsAsync(assignedGroupIds, cancellationToken);
            var topicIds = groups.Values.Select(g => g.TopicId).Distinct().ToList();
            var topics = await topicDefenseContract.GetTopicsDetailsAsync(topicIds, cancellationToken);

            foreach (var topic in topics.Values)
            {
                if (memberUserIds.Contains(topic.AdvisorId))
                {
                    var advisorName = usersInfo.TryGetValue(topic.AdvisorId, out var u) ? u.FullName : topic.AdvisorId.ToString();
                    return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(
                        CouncilErrors.MemberIsGroupAdvisor(advisorName, topic.TitleVi));
                }
            }

            // Reviewer in Council role check: If a member is a Reviewer for any assigned group, they CAN ONLY be a Member
            var activeReviewers = await councilRepository.GetActiveReviewerIdsByGroupIdsAsync(assignedGroupIds, cancellationToken);
            foreach (var memberInput in command.Members)
            {
                if (memberInput.Role != CouncilMemberRole.Member)
                {
                    var matchedAssignment = activeReviewers.FirstOrDefault(kvp => kvp.Value == memberInput.UserId);
                    if (matchedAssignment.Key != Guid.Empty)
                    {
                        var reviewerName = usersInfo.TryGetValue(memberInput.UserId, out var u) ? u.FullName : memberInput.UserId.ToString();
                        var topicTitle = groups.TryGetValue(matchedAssignment.Key, out var grp) && topics.TryGetValue(grp.TopicId, out var top)
                            ? top.TitleVi
                            : matchedAssignment.Key.ToString();

                        return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(
                            CouncilErrors.MemberIsGroupReviewerWithInvalidRole(reviewerName, topicTitle));
                    }
                }
            }
        }

        // 2. Schedule conflict validation: check if lecturer is busy in another council on same date + timeSlot
        var conflictingUserIds = await councilRepository.GetConflictingMemberUserIdsAsync(
            council.DefenseDate,
            council.TimeSlot,
            memberUserIds,
            council.Id,
            cancellationToken);

        if (conflictingUserIds.Count > 0)
        {
            var conflictNames = string.Join(", ", conflictingUserIds.Select(id =>
                usersInfo.TryGetValue(id, out var u) ? $"{u.FullName} ({u.UserCode ?? id.ToString()})" : id.ToString()));
            return Result.Failure<IReadOnlyList<CouncilMemberResponseDto>>(
                CouncilErrors.MemberScheduleConflict(conflictNames));
        }

        // Add members
        var newMembers = command.Members.Select(m => new CouncilMember
        {
            Id = Guid.NewGuid(),
            CouncilId = council.Id,
            UserId = m.UserId,
            RoleInCouncil = m.Role
        }).ToList();

        await councilRepository.AddMembersAsync(newMembers, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var resultDtos = newMembers.Select(m =>
        {
            var info = usersInfo.GetValueOrDefault(m.UserId);
            return new CouncilMemberResponseDto(
                m.Id,
                m.CouncilId,
                m.UserId,
                m.RoleInCouncil.ToString(),
                info?.FullName,
                info?.Email,
                info?.UserCode);
        }).ToList();

        return Result.Success<IReadOnlyList<CouncilMemberResponseDto>>(resultDtos);
    }
}
