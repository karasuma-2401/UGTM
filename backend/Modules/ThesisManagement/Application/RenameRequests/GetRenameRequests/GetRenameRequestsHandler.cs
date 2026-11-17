using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.GetRenameRequests;

public sealed class GetRenameRequestsHandler(
    IRenameRequestRepository renameRequestRepository,
    IStudentGroupRepository groupRepository,
    ITopicSummaryReader topicSummaryReader,
    IBatchSummaryReader batchSummaryReader,
    IStudentInfoReader studentInfoReader,
    IAdvisorNameReader advisorNameReader,
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
        var isLecturer = currentUser.Roles.Any(r =>
            string.Equals(r, "Lecturer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(r, "Advisor", StringComparison.OrdinalIgnoreCase));

        if (!isPrivilegedStaff && !isLecturer)
        {
            return Result.Failure<IReadOnlyList<RenameRequestDto>>(RenameRequestErrors.AccessDenied);
        }

        RenameRequestStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<RenameRequestStatus>(query.Status.Trim(), ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return Result.Failure<IReadOnlyList<RenameRequestDto>>(RenameRequestErrors.InvalidRequest);
            }

            statusFilter = parsed;
        }

        var requests = await renameRequestRepository.ListAsync(query.GroupId, statusFilter, null, cancellationToken);
        if (requests.Count == 0)
        {
            return Result.Success<IReadOnlyList<RenameRequestDto>>([]);
        }

        var groupsById = new Dictionary<Guid, StudentGroup>();
        foreach (var request in requests)
        {
            if (request.Group is not null)
            {
                groupsById.TryAdd(request.GroupId, request.Group);
            }
            else if (!groupsById.ContainsKey(request.GroupId))
            {
                var group = await groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
                if (group is not null)
                {
                    groupsById[request.GroupId] = group;
                }
            }
        }

        var topicIds = groupsById.Values
            .Select(g => g.TopicId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var topicsById = topicIds.Count > 0
            ? await topicSummaryReader.GetByIdsAsync(topicIds, cancellationToken)
            : new Dictionary<Guid, TopicSummaryContractDto>();

        IReadOnlyList<RenameRequest> visibleRequests;
        if (isPrivilegedStaff)
        {
            visibleRequests = requests;
        }
        else
        {
            var ownRequests = new List<RenameRequest>(requests.Count);
            foreach (var request in requests)
            {
                if (groupsById.TryGetValue(request.GroupId, out var group)
                    && topicsById.TryGetValue(group.TopicId, out var topic)
                    && topic.AdvisorId == currentUser.UserId.Value)
                {
                    ownRequests.Add(request);
                }
            }

            visibleRequests = ownRequests;
        }

        if (visibleRequests.Count == 0)
        {
            return Result.Success<IReadOnlyList<RenameRequestDto>>([]);
        }

        var visibleTopics = visibleRequests
            .Select(r => groupsById.TryGetValue(r.GroupId, out var g) && topicsById.TryGetValue(g.TopicId, out var t) ? t : null)
            .Where(t => t is not null)
            .Cast<TopicSummaryContractDto>()
            .ToList();

        var batchIds = visibleTopics.Select(t => t.BatchId).Where(id => id != Guid.Empty).Distinct().ToList();
        var advisorIds = visibleTopics.Select(t => t.AdvisorId).Where(id => id != Guid.Empty).Distinct().ToList();
        var studentIds = visibleRequests
            .SelectMany(r =>
            {
                var confirmedIds = groupsById.TryGetValue(r.GroupId, out var g)
                    ? g.Members.Where(m => m.ConfirmationStatus == MemberConfirmationStatus.Confirmed).Select(m => m.StudentId)
                    : [];
                return confirmedIds.Append(r.RequestedByStudentId);
            })
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var batchesById = batchIds.Count > 0
            ? await batchSummaryReader.GetByIdsAsync(batchIds, cancellationToken)
            : new Dictionary<Guid, BatchSummaryContractDto>();
        var advisorsById = advisorIds.Count > 0
            ? await advisorNameReader.GetNamesByIdsAsync(advisorIds, cancellationToken)
            : new Dictionary<Guid, string>();
        var studentsById = studentIds.Count > 0
            ? await studentInfoReader.GetStudentsByIdsAsync(studentIds, cancellationToken)
            : new Dictionary<Guid, StudentInfoContractDto>();

        var dtos = new List<RenameRequestDto>(visibleRequests.Count);
        foreach (var request in visibleRequests)
        {
            TopicSummaryContractDto? topic = null;
            List<RenameRequestMemberDto>? members = null;

            if (groupsById.TryGetValue(request.GroupId, out var group))
            {
                topicsById.TryGetValue(group.TopicId, out topic);
                var confirmedMembers = group.Members
                    .Where(m => m.ConfirmationStatus == MemberConfirmationStatus.Confirmed)
                    .OrderByDescending(m => m.StudentId == request.RequestedByStudentId)
                    .Select(m =>
                    {
                        studentsById.TryGetValue(m.StudentId, out var info);
                        return new RenameRequestMemberDto(m.StudentId, info?.UserCode, info?.FullName);
                    })
                    .ToList();

                if (confirmedMembers.Count > 0)
                {
                    members = confirmedMembers;
                }
            }

            string? batchName = topic is not null && batchesById.TryGetValue(topic.BatchId, out var batch)
                ? batch.BatchName
                : null;
            string? advisorName = topic is not null && advisorsById.TryGetValue(topic.AdvisorId, out var advisor)
                ? advisor
                : null;

            dtos.Add(RenameRequestMapper.ToDto(
                request,
                topic?.BatchId,
                batchName,
                topic?.AdvisorId,
                advisorName,
                members));
        }

        return Result.Success<IReadOnlyList<RenameRequestDto>>(dtos);
    }
}
