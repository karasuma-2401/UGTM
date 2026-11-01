using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;

public sealed record GetCouncilScheduleQuery(Guid Id);

public sealed class GetCouncilScheduleHandler(
    ICouncilRepository councilRepository,
    IDefenseGroupContract defenseGroupContract,
    ITopicDefenseContract topicDefenseContract,
    ICouncilUserInfoReader userInfoReader,
    IStudentInfoReader studentInfoReader,
    ICurrentUser currentUser)
{
    public async Task<Result<CouncilScheduleResponseDto>> HandleAsync(
        GetCouncilScheduleQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<CouncilScheduleResponseDto>(CouncilErrors.Forbidden);
        }

        // 1. Resolve Council by GroupId, StudentId, or CouncilId
        Council? council = null;

        // Try GroupId
        council = await councilRepository.GetCouncilByGroupIdAsync(query.Id, cancellationToken);

        // Try StudentId
        if (council is null)
        {
            var studentGroup = await defenseGroupContract.GetActiveGroupByStudentIdAsync(query.Id, cancellationToken);
            if (studentGroup is not null)
            {
                council = await councilRepository.GetCouncilByGroupIdAsync(studentGroup.GroupId, cancellationToken);
            }
        }

        // Try CouncilId
        council ??= await councilRepository.GetByIdWithDetailsAsync(query.Id, cancellationToken);

        if (council is null)
        {
            return Result.Failure<CouncilScheduleResponseDto>(CouncilErrors.NotFound);
        }

        // 2. Check visibility for Student role
        var isStudentOnly = currentUser.Roles.Contains("Student")
                            && !currentUser.Roles.Any(r => r is "Admin" or "AcademicAffairs" or "Faculty" or "Lecturer" or "CouncilMember");

        if (isStudentOnly && council.Status == CouncilStatus.Draft)
        {
            return Result.Failure<CouncilScheduleResponseDto>(CouncilErrors.ScheduleNotPublished);
        }

        // 3. Fetch Council Members detail
        var memberUserIds = council.Members.Select(m => m.UserId).Distinct().ToList();
        var memberUsersInfo = await userInfoReader.GetUsersByIdsAsync(memberUserIds, cancellationToken);

        var memberDtos = council.Members.Select(m =>
        {
            var info = memberUsersInfo.GetValueOrDefault(m.UserId);
            return new CouncilScheduleMemberDto(
                m.UserId,
                info?.FullName ?? m.UserId.ToString(),
                info?.Email ?? string.Empty,
                info?.UserCode,
                m.RoleInCouncil.ToString());
        }).ToList();

        // 4. Fetch Groups, Topics, Advisors, Reviewers, and Students
        var assignments = council.Assignments.OrderBy(a => a.OrderNumber).ToList();
        var assignedGroupIds = assignments.Select(a => a.GroupId).Distinct().ToList();

        var groups = await defenseGroupContract.GetGroupsByIdsAsync(assignedGroupIds, cancellationToken);
        var topicIds = groups.Values.Select(g => g.TopicId).Distinct().ToList();
        var topics = await topicDefenseContract.GetTopicsDetailsAsync(topicIds, cancellationToken);
        var activeReviewerMap = await councilRepository.GetActiveReviewerIdsByGroupIdsAsync(assignedGroupIds, cancellationToken);

        // Collect all advisor and reviewer user IDs
        var lecturerIdsToFetch = new HashSet<Guid>();
        foreach (var topic in topics.Values)
        {
            lecturerIdsToFetch.Add(topic.AdvisorId);
        }
        foreach (var reviewerId in activeReviewerMap.Values)
        {
            lecturerIdsToFetch.Add(reviewerId);
        }

        var lecturerUsersInfo = await userInfoReader.GetUsersByIdsAsync(lecturerIdsToFetch, cancellationToken);

        // Collect all student IDs across assigned groups
        var allStudentIds = groups.Values
            .SelectMany(g => g.Members.Select(m => m.StudentId))
            .Distinct()
            .ToList();

        var studentsInfo = await studentInfoReader.GetStudentsByIdsAsync(allStudentIds, cancellationToken);

        var groupDtos = assignments.Select(a =>
        {
            groups.TryGetValue(a.GroupId, out var groupSnapshot);
            var topicId = groupSnapshot?.TopicId ?? Guid.Empty;
            topics.TryGetValue(topicId, out var topic);

            var advisorId = topic?.AdvisorId ?? Guid.Empty;
            var advisorName = lecturerUsersInfo.TryGetValue(advisorId, out var adv)
                ? adv.FullName
                : advisorId != Guid.Empty ? advisorId.ToString() : null;

            activeReviewerMap.TryGetValue(a.GroupId, out var reviewerId);
            string? reviewerName = null;
            var isReviewerInCouncil = false;

            if (reviewerId != Guid.Empty)
            {
                if (lecturerUsersInfo.TryGetValue(reviewerId, out var rev))
                {
                    reviewerName = rev.FullName;
                }
                else
                {
                    reviewerName = reviewerId.ToString();
                }

                isReviewerInCouncil = memberUserIds.Contains(reviewerId);
            }

            var groupStudents = groupSnapshot?.Members.Select(m =>
            {
                var studentInfo = studentsInfo.GetValueOrDefault(m.StudentId);
                return new CouncilScheduleStudentDto(
                    m.StudentId,
                    studentInfo?.FullName ?? m.StudentId.ToString(),
                    studentInfo?.Email ?? string.Empty,
                    studentInfo?.UserCode);
            }).ToList() ?? [];

            return new CouncilScheduleGroupDto(
                a.GroupId,
                a.OrderNumber,
                topicId,
                topic?.TitleVi ?? string.Empty,
                topic?.TitleEn,
                advisorId,
                advisorName,
                reviewerId != Guid.Empty ? reviewerId : null,
                reviewerName,
                isReviewerInCouncil,
                groupStudents);
        }).ToList();

        return Result.Success(new CouncilScheduleResponseDto(
            council.Id,
            council.BatchId,
            council.CouncilCode,
            council.DefenseDate,
            council.TimeSlot,
            council.Room,
            council.Status.ToString(),
            council.DecisionNumber,
            council.SignedDate,
            memberDtos,
            groupDtos));
    }
}
