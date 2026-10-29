using Microsoft.Extensions.Logging;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.StudentGroups.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Application.StudentGroups.GetStudentGroupDetail;

public sealed class GetStudentGroupDetailHandler(
    IStudentGroupDetailReader groupReader,
    ITopicSummaryReader topicReader,
    IBatchSummaryReader batchReader,
    IStudentInfoReader studentReader,
    IAdvisorNameReader advisorReader,
    ICurrentUser currentUser,
    ILogger<GetStudentGroupDetailHandler> logger)
{
    public async Task<Result<StudentGroupDetailDto>> HandleAsync(
        GetStudentGroupDetailQuery query, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure<StudentGroupDetailDto>(StudentGroupDetailErrors.AuthenticationRequired);
        }
        if (query.GroupId == Guid.Empty)
        {
            return Result.Failure<StudentGroupDetailDto>(StudentGroupDetailErrors.InvalidRequest);
        }

        var group = await groupReader.GetByIdAsync(query.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<StudentGroupDetailDto>(StudentGroupDetailErrors.GroupNotFound);
        }

        var topics = await topicReader.GetByIdsAsync([group.TopicId], cancellationToken);
        topics.TryGetValue(group.TopicId, out var topic);
        // Authorize against the same topic snapshot used in the response, before reading user details.
        if (!CanRead(group, topic?.AdvisorId))
        {
            return Result.Failure<StudentGroupDetailDto>(StudentGroupDetailErrors.AccessDenied);
        }
        if (topic is null || topic.BatchId == Guid.Empty || topic.AdvisorId == Guid.Empty
            || group.Members.Any(member => member.BatchId != topic.BatchId))
        {
            logger.LogError("Group {GroupId} has missing topic data or inconsistent batch links.", group.Id);
            return Result.Failure<StudentGroupDetailDto>(StudentGroupDetailErrors.InconsistentData);
        }

        var batches = await batchReader.GetByIdsAsync([topic.BatchId], cancellationToken);
        if (!batches.TryGetValue(topic.BatchId, out var batch))
        {
            logger.LogWarning("Batch {BatchId} for group {GroupId} was not found.", topic.BatchId, group.Id);
        }
        var advisorNames = await advisorReader.GetNamesByIdsAsync([topic.AdvisorId], cancellationToken);
        if (!advisorNames.TryGetValue(topic.AdvisorId, out var advisorName))
        {
            logger.LogWarning("Advisor {AdvisorId} for group {GroupId} was not found.", topic.AdvisorId, group.Id);
        }
        var students = await studentReader.GetStudentsByIdsAsync(
            group.Members.Select(member => member.StudentId), cancellationToken);
        var members = group.Members.OrderBy(member => member.JoinedAt).ThenBy(member => member.StudentId)
            .Select(member =>
            {
                if (!students.TryGetValue(member.StudentId, out var student))
                {
                    logger.LogWarning("Student {StudentId} for group {GroupId} was not found.", member.StudentId, group.Id);
                }
                return new StudentGroupMemberDto(member.StudentId, student?.UserCode, student?.FullName,
                    student?.Email, member.ConfirmationStatus.ToString());
            }).ToArray();

        return Result.Success(new StudentGroupDetailDto(group.Id, topic.TopicId, topic.TitleVi, topic.TitleEn,
            topic.BatchId, batch?.BatchName, new StudentGroupAdvisorDto(topic.AdvisorId, advisorName), members,
            group.Status.ToString()));
    }

    private bool CanRead(StudentGroup group, Guid? advisorId)
    {
        bool HasRole(string role) => currentUser.Roles.Any(value => string.Equals(value, role, StringComparison.OrdinalIgnoreCase));
        return ThesisAuthorization.IsAdministrator(currentUser)
            || (HasRole("Faculty") && ThesisAuthorization.CanReviewGates(currentUser))
            || (HasRole("Student") && group.Members.Any(member =>
                member.StudentId == currentUser.UserId && member.ConfirmationStatus == MemberConfirmationStatus.Confirmed))
            || advisorId == currentUser.UserId;
    }
}
