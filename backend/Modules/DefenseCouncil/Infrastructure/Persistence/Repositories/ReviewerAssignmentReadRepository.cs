using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.AcademicPeriods.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.Identity.Contracts.ReviewerAssignments;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerAssignmentReadRepository(DefenseCouncilDbContext context,
    IReviewerAssignmentThesisReader thesis, IReviewerAssignmentTopicReader topics, IReviewerAssignmentBatchReader batches,
    IReviewerCandidateReader candidates, IAdvisorNameReader names) : IReviewerAssignmentReadRepository
{
    public async Task<PagedResult<ReviewerAssignmentListItemDto>> ListAsync(ReviewerAssignmentListQuery request, bool canManage, CancellationToken token)
    {
        var query = context.ReviewerAssignments.AsNoTracking().AsQueryable();
        if (request.GroupId.HasValue) query = query.Where(value => value.GroupId == request.GroupId.Value);
        if (request.ReviewerId.HasValue) query = query.Where(value => value.ReviewerId == request.ReviewerId.Value);
        var active = request.IsActive ?? (request.IncludeHistory ? (bool?)null : true);
        if (active.HasValue) query = query.Where(value => value.IsActive == active.Value);
        query = await FilterAsync(query, request.BatchId, request.Keyword, true, token);
        var total = await query.CountAsync(token);
        var skip = ((long)request.PageNumber - 1) * request.PageSize;
        if (skip >= total) return new([], total, request.PageNumber, request.PageSize);
        var page = await query.OrderByDescending(value => value.AssignedAt).ThenBy(value => value.Id).Skip((int)skip).Take(request.PageSize)
            .Select(value => new { Assignment = value, HasEvaluation = context.ReviewerEvaluations.Any(sheet => sheet.AssignmentId == value.Id) })
            .ToListAsync(token);
        var links = (await thesis.GetGroupLinksAsync(page.Select(value => value.Assignment.GroupId).Distinct().ToArray(), null, token)).ToDictionary(value => value.GroupId);
        var topicMap = (await topics.GetManyAsync(links.Values.Select(value => value.TopicId).Distinct().ToArray(), token)).ToDictionary(value => value.TopicId);
        var nameMap = await names.GetNamesByIdsAsync(page.Select(value => value.Assignment.ReviewerId), token);
        var items = page.Select(value =>
        {
            if (!links.TryGetValue(value.Assignment.GroupId, out var link) || !topicMap.TryGetValue(link.TopicId, out var topic))
                throw new InvalidOperationException("Reviewer assignment group/topic foreign key context is missing.");
            return new ReviewerAssignmentListItemDto(ReviewerAssignmentDtoMapper.ToDto(value.Assignment, topic,
                nameMap.GetValueOrDefault(value.Assignment.ReviewerId)), value.HasEvaluation, canManage && value.Assignment.IsActive && !value.HasEvaluation);
        }).ToArray();
        return new(items, total, request.PageNumber, request.PageSize);
    }

    public async Task<PagedResult<ReviewerAssignmentEligibilityItemDto>> EligibilityAsync(ReviewerAssignmentEligibilityQuery request, bool canManage, CancellationToken token)
    {
        if (await batches.GetAsync(request.BatchId, token) is not { IsOpen: true }) return new([], 0, request.PageNumber, request.PageSize);
        var topicIds = await topics.FindIdsAsync(request.BatchId, request.Keyword, token);
        var groups = await thesis.GetByTopicIdsAsync(topicIds.ToArray(), token);
        if (groups.Count == 0) return new([], 0, request.PageNumber, request.PageSize);
        var topicMap = (await topics.GetManyAsync(groups.Select(value => value.TopicId).Distinct().ToArray(), token)).ToDictionary(value => value.TopicId);
        var ids = groups.Select(value => value.GroupId).ToArray();
        var approvals = await ApprovedAsync(ids, token);
        var active = await context.ReviewerAssignments.AsNoTracking().Where(value => ids.Contains(value.GroupId) && value.IsActive)
            .Select(value => new { value.GroupId, value.Id }).ToDictionaryAsync(value => value.GroupId, value => value.Id, token);
        var eligible = groups.Where(group => topicMap.TryGetValue(group.TopicId, out var topic)
            && ContextValid(group, topic) && approvals.TryGetValue(group.GroupId, out var approval) && BindingValid(group, approval)
            && (request.IncludeAssigned || !active.ContainsKey(group.GroupId)))
            .OrderBy(group => approvals[group.GroupId].ApprovedAtUtc).ThenBy(group => group.GroupId).ToArray();
        var skip = ((long)request.PageNumber - 1) * request.PageSize;
        var page = skip >= eligible.Length ? [] : eligible.Skip((int)skip).Take(request.PageSize).ToArray();
        var nameMap = await names.GetNamesByIdsAsync(page.Select(group => topicMap[group.TopicId].AdvisorId), token);
        var items = page.Select(group =>
        {
            var topic = topicMap[group.TopicId];
            var approval = approvals[group.GroupId];
            var hasActive = active.TryGetValue(group.GroupId, out var assignmentId);
            return new ReviewerAssignmentEligibilityItemDto(group.GroupId, topic.TopicId, topic.BatchId, topic.TitleVi, topic.TitleEn,
                topic.AdvisorId, nameMap.GetValueOrDefault(topic.AdvisorId), approval.AdvisorEvaluationId, approval.FinalReportId,
                approval.SubmissionFileId, approval.ReportVersionNo, approval.SubmissionChecksumSha256, approval.ApprovedAtUtc,
                hasActive, hasActive ? assignmentId : null, canManage && !hasActive);
        }).ToArray();
        return new(items, eligible.Length, request.PageNumber, request.PageSize);
    }

    public async Task<PagedResult<ReviewerAssignmentTaskDto>> TasksAsync(Guid reviewerId, ReviewerAssignmentTaskQuery request, CancellationToken token)
    {
        var query = context.ReviewerAssignments.AsNoTracking().Where(value => value.ReviewerId == reviewerId && value.IsActive);
        query = await FilterAsync(query, request.BatchId, request.Keyword, false, token);
        var total = await query.CountAsync(token);
        var skip = ((long)request.PageNumber - 1) * request.PageSize;
        if (skip >= total) return new([], total, request.PageNumber, request.PageSize);
        var page = await query.OrderByDescending(value => value.AssignedAt).ThenBy(value => value.Id).Skip((int)skip).Take(request.PageSize).ToListAsync(token);
        var groups = (await thesis.GetManyAsync(page.Select(value => value.GroupId).Distinct().ToArray(), token)).ToDictionary(value => value.GroupId);
        var topicMap = (await topics.GetManyAsync(groups.Values.Select(value => value.TopicId).Distinct().ToArray(), token)).ToDictionary(value => value.TopicId);
        var batchIds = (await batches.GetManyAsync(topicMap.Values.Select(value => value.BatchId).Distinct().ToArray(), token)).Select(value => value.BatchId).ToHashSet();
        var approvals = await ApprovedAsync(page.Select(value => value.GroupId).ToArray(), token);
        var items = page.Select(assignment =>
        {
            var group = groups.GetValueOrDefault(assignment.GroupId);
            var topic = group is null ? null : topicMap.GetValueOrDefault(group.TopicId);
            string? block = assignment.IsLegacy ? "LegacyRecordNeedsResolution"
                : group is null || topic is null || !ContextValid(group, topic) || !batchIds.Contains(topic.BatchId) ? "ContextInvalid"
                : assignment.ReviewerId == topic.AdvisorId ? "AdvisorConflict"
                : !approvals.TryGetValue(assignment.GroupId, out var approval) || !BindingValid(group, approval)
                    || assignment.AdvisorEvaluationId != approval.AdvisorEvaluationId || assignment.FinalReportId != approval.FinalReportId
                    || assignment.ReportVersionNo != approval.ReportVersionNo || assignment.SubmissionFileId != approval.SubmissionFileId
                    || !string.Equals(assignment.SubmissionChecksumSha256, approval.SubmissionChecksumSha256, StringComparison.OrdinalIgnoreCase)
                    ? "BindingInvalid" : null;
            if (group is null || topic is null) throw new InvalidOperationException("Reviewer task group/topic foreign key context is missing.");
            return new ReviewerAssignmentTaskDto(assignment.Id, assignment.GroupId, topic.TopicId, topic.BatchId, topic.TitleVi, topic.TitleEn,
                assignment.AssignedAt, assignment.Version, assignment.IsLegacy, assignment.FinalReportId, assignment.ReportVersionNo,
                assignment.SubmissionFileId, block is null ? group.ReportFile : null, block is null, block);
        }).ToArray();
        return new(items, total, request.PageNumber, request.PageSize);
    }

    private async Task<IQueryable<ReviewerAssignment>> FilterAsync(IQueryable<ReviewerAssignment> query, Guid? batchId,
        string? keyword, bool searchReviewerNames, CancellationToken token)
    {
        if (batchId.HasValue)
        {
            var batchTopics = await topics.FindIdsAsync(batchId, null, token);
            var groupIds = (await thesis.GetGroupLinksAsync(null, batchTopics.ToArray(), token)).Select(value => value.GroupId).ToArray();
            query = query.Where(value => groupIds.Contains(value.GroupId));
        }
        if (!string.IsNullOrEmpty(keyword))
        {
            var matchingTopics = await topics.FindIdsAsync(batchId, keyword, token);
            var groupIds = (await thesis.GetGroupLinksAsync(null, matchingTopics.ToArray(), token)).Select(value => value.GroupId).ToArray();
            var userIds = searchReviewerNames ? (await candidates.FindUserIdsAsync(keyword, token)).ToArray() : [];
            query = query.Where(value => groupIds.Contains(value.GroupId) || userIds.Contains(value.ReviewerId));
        }
        return query;
    }

    private async Task<Dictionary<Guid, ReviewerAssignmentEligibilityContractDto>> ApprovedAsync(Guid[] groupIds, CancellationToken token)
    {
        var rows = await context.AdvisorEvaluations.AsNoTracking().Where(value => groupIds.Contains(value.GroupId)
            && !value.IsLegacy && value.AllowsReviewerAssignment == true && value.FinalReportId != null
            && value.ReportVersionNo > 0 && value.SubmissionFileId != null && value.SubmissionChecksumSha256 != null && value.DecisionAt != null)
            .Select(value => new ReviewerAssignmentEligibilityContractDto(value.Id, value.GroupId, value.FinalReportId!.Value,
                value.ReportVersionNo!.Value, value.SubmissionFileId!.Value, value.SubmissionChecksumSha256!, value.DecisionAt!.Value))
            .ToListAsync(token);
        return rows.Where(value => value.SubmissionChecksumSha256.Length == 64 && value.SubmissionChecksumSha256.All(Uri.IsHexDigit))
            .ToDictionary(value => value.GroupId);
    }

    private static bool ContextValid(ReviewerAssignmentThesisContractDto group, ReviewerAssignmentTopicContractDto topic)
        => group.TopicId == topic.TopicId && group.BatchId == topic.BatchId && topic.AdvisorId != Guid.Empty;

    private static bool BindingValid(ReviewerAssignmentThesisContractDto group, ReviewerAssignmentEligibilityContractDto approval)
        => group.IsReady && group.IsUnderEvaluation && group.ReportFile is not null && group.GroupId == approval.GroupId
            && group.FinalReportId == approval.FinalReportId && group.ReportVersionNo == approval.ReportVersionNo
            && group.SubmissionFileId == approval.SubmissionFileId
            && string.Equals(group.SubmissionChecksumSha256, approval.SubmissionChecksumSha256, StringComparison.OrdinalIgnoreCase);
}
