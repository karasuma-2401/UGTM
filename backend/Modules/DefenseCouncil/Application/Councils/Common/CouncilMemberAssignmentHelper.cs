using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.Common;

internal sealed record ValidatedCouncilMembersResult(
    List<CouncilMember> NewMembers,
    List<Guid> MemberUserIds,
    IReadOnlyDictionary<Guid, CouncilUserInfoContractDto> UsersInfo);

internal sealed record CouncilAssignmentRolesInfo(
    Dictionary<Guid, string> AdvisorTopicsByUserId,
    Dictionary<Guid, string> ReviewerTopicsByUserId);

/// <summary>
/// Cung cấp logic xác thực ràng buộc phân công thành viên hội đồng (chống xung đột lợi ích, trùng lịch, vai trò Giảng viên)
/// dùng chung giữa các handler thêm, cập nhật, gỡ và tra cứu ứng viên.
/// </summary>
internal static class CouncilMemberAssignmentHelper
{
    internal static bool IsCouncilStaffOrAdmin(ICurrentUser currentUser)
        => currentUser.IsAuthenticated
           && currentUser.Roles.Any(r => r is "Admin" or "AcademicAffairs" or "Faculty");

    internal static async Task<CouncilAssignmentRolesInfo> ResolveAssignmentRolesAsync(
        IReadOnlyCollection<CouncilAssignment> assignments,
        ICouncilRepository councilRepository,
        IDefenseGroupContract defenseGroupContract,
        ITopicDefenseContract topicDefenseContract,
        CancellationToken cancellationToken)
    {
        var advisorTopics = new Dictionary<Guid, string>();
        var reviewerTopics = new Dictionary<Guid, string>();

        if (assignments.Count == 0)
        {
            return new CouncilAssignmentRolesInfo(advisorTopics, reviewerTopics);
        }

        var assignedGroupIds = assignments.Select(a => a.GroupId).Distinct().ToList();
        var groups = await defenseGroupContract.GetGroupsByIdsAsync(assignedGroupIds, cancellationToken);
        var topicIds = groups.Values.Select(g => g.TopicId).Distinct().ToList();
        var topics = await topicDefenseContract.GetTopicsDetailsAsync(topicIds, cancellationToken);

        foreach (var topic in topics.Values)
        {
            advisorTopics.TryAdd(topic.AdvisorId, topic.TitleVi);
        }

        var activeReviewers = await councilRepository.GetActiveReviewerIdsByGroupIdsAsync(assignedGroupIds, cancellationToken);
        foreach (var (groupId, reviewerId) in activeReviewers)
        {
            var topicTitle = groups.TryGetValue(groupId, out var grp) && topics.TryGetValue(grp.TopicId, out var top)
                ? top.TitleVi
                : groupId.ToString();

            reviewerTopics.TryAdd(reviewerId, topicTitle);
        }

        return new CouncilAssignmentRolesInfo(advisorTopics, reviewerTopics);
    }

    internal static Result<bool> ValidateMemberInputList(IReadOnlyList<CouncilMemberInputDto> members)
    {
        if (members.Count == 0)
        {
            return Result.Failure<bool>(
                Error.Validation("Council.EmptyMembers", "Danh sách thành viên không được để trống."));
        }

        if (members.Any(member => member is null || member.UserId == Guid.Empty || !Enum.IsDefined(member.Role)))
        {
            return Result.Failure<bool>(
                Error.Validation("Council.InvalidMember", "Thành viên và vai trò hội đồng phải hợp lệ."));
        }

        return Result.Success(true);
    }

    internal static async Task<Result<ValidatedCouncilMembersResult>> ValidateForCouncilAsync(
        Council council,
        IReadOnlyList<CouncilMemberInputDto> requestedMembers,
        bool isReplaceMode,
        ICouncilRepository councilRepository,
        ICouncilUserInfoReader userInfoReader,
        IDefenseGroupContract defenseGroupContract,
        ITopicDefenseContract topicDefenseContract,
        CancellationToken cancellationToken,
        IReadOnlyCollection<CouncilAssignment>? assignmentOverride = null)
    {
        if (council.Status != CouncilStatus.Draft)
        {
            return Result.Failure<ValidatedCouncilMembersResult>(CouncilErrors.NotInDraftStatus);
        }

        if (requestedMembers.Select(m => m.UserId).Distinct().Count() != requestedMembers.Count)
        {
            return Result.Failure<ValidatedCouncilMembersResult>(
                Error.Conflict("Council.DuplicateMemberInRequest", "Danh sách thành viên gửi lên có giảng viên bị lặp lại."));
        }

        var existingChairmanCount = isReplaceMode ? 0 : council.Members.Count(m => m.RoleInCouncil == CouncilMemberRole.Chairman);
        var existingSecretaryCount = isReplaceMode ? 0 : council.Members.Count(m => m.RoleInCouncil == CouncilMemberRole.Secretary);

        var newChairmanCount = requestedMembers.Count(m => m.Role == CouncilMemberRole.Chairman);
        var newSecretaryCount = requestedMembers.Count(m => m.Role == CouncilMemberRole.Secretary);

        if (existingChairmanCount + newChairmanCount > 1)
        {
            return Result.Failure<ValidatedCouncilMembersResult>(CouncilErrors.DuplicateChairman);
        }

        if (existingSecretaryCount + newSecretaryCount > 1)
        {
            return Result.Failure<ValidatedCouncilMembersResult>(CouncilErrors.DuplicateSecretary);
        }

        var memberUserIds = requestedMembers.Select(m => m.UserId).Distinct().ToList();

        if (!isReplaceMode)
        {
            var existingUserIds = council.Members.Select(m => m.UserId).ToHashSet();
            var duplicateExistingUser = memberUserIds.FirstOrDefault(id => existingUserIds.Contains(id));
            if (duplicateExistingUser != Guid.Empty)
            {
                return Result.Failure<ValidatedCouncilMembersResult>(
                    CouncilErrors.DuplicateMember(duplicateExistingUser.ToString()));
            }
        }

        var usersInfo = await userInfoReader.GetUsersByIdsAsync(memberUserIds, cancellationToken);
        foreach (var userId in memberUserIds)
        {
            if (!usersInfo.TryGetValue(userId, out var user) || !user.IsActive)
            {
                return Result.Failure<ValidatedCouncilMembersResult>(CouncilErrors.MemberNotFound(userId));
            }

            if (!user.IsLecturer)
            {
                return Result.Failure<ValidatedCouncilMembersResult>(CouncilErrors.MemberNotLecturer(user.FullName));
            }
        }

        var assignments = assignmentOverride ?? council.Assignments.ToList();
        if (assignments.Count > 0)
        {
            var rolesInfo = await ResolveAssignmentRolesAsync(
                assignments,
                councilRepository,
                defenseGroupContract,
                topicDefenseContract,
                cancellationToken);

            foreach (var userId in memberUserIds)
            {
                if (rolesInfo.AdvisorTopicsByUserId.TryGetValue(userId, out var topicTitle))
                {
                    var advisorName = usersInfo.TryGetValue(userId, out var u) ? u.FullName : userId.ToString();
                    return Result.Failure<ValidatedCouncilMembersResult>(
                        CouncilErrors.MemberIsGroupAdvisor(advisorName, topicTitle));
                }
            }

            foreach (var memberInput in requestedMembers)
            {
                if (memberInput.Role != CouncilMemberRole.Member
                    && rolesInfo.ReviewerTopicsByUserId.TryGetValue(memberInput.UserId, out var topicTitle))
                {
                    var reviewerName = usersInfo.TryGetValue(memberInput.UserId, out var u) ? u.FullName : memberInput.UserId.ToString();
                    return Result.Failure<ValidatedCouncilMembersResult>(
                        CouncilErrors.MemberIsGroupReviewerWithInvalidRole(reviewerName, topicTitle));
                }
            }
        }

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
            return Result.Failure<ValidatedCouncilMembersResult>(
                CouncilErrors.MemberScheduleConflict(conflictNames));
        }

        var newMembers = requestedMembers.Select(m => new CouncilMember
        {
            Id = Guid.NewGuid(),
            CouncilId = council.Id,
            UserId = m.UserId,
            RoleInCouncil = m.Role
        }).ToList();

        return Result.Success(new ValidatedCouncilMembersResult(newMembers, memberUserIds, usersInfo));
    }

    internal static IReadOnlyList<CouncilMemberResponseDto> MapMemberDtos(
        IEnumerable<CouncilMember> members,
        IReadOnlyDictionary<Guid, CouncilUserInfoContractDto> usersInfo)
        => members.Select(m =>
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
}
