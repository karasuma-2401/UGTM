using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Contracts.Councils;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;

public sealed record AssignedCouncilSimilarityReportDto(Guid GroupId, Guid FinalReportId, int VersionNo, string TopicTitle);

public sealed class GetMyCouncilSimilarityReportsHandler(
    ICurrentUser currentUser,
    ICouncilAssignedGroupReader assignedGroups,
    IStudentGroupRepository groups,
    IFinalReportRepository reports,
    ITopicSummaryReader topics)
{
    public async Task<Result<IReadOnlyList<AssignedCouncilSimilarityReportDto>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty
            || !currentUser.Roles.Any(role => string.Equals(role, "CouncilMember", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<IReadOnlyList<AssignedCouncilSimilarityReportDto>>(PlagiarismScanErrors.AccessDenied);
        }

        var assigned = await assignedGroups.GetAssignedGroupIdsAsync(currentUser.UserId.Value, cancellationToken);
        var items = new List<(Guid GroupId, Guid TopicId, Guid ReportId, int VersionNo)>();
        foreach (var groupId in assigned.Distinct())
        {
            var group = await groups.GetByIdAsync(groupId, cancellationToken);
            if (group is null) continue;
            foreach (var kind in Enum.GetValues<FinalReportVersionType>())
            {
                var report = await reports.GetByGroupAndVersionTypeAsync(groupId, kind, cancellationToken);
                if (report?.Status == FinalReportStatus.UnderEvaluation)
                    items.Add((groupId, group.TopicId, report.Id, report.CurrentVersionNo));
            }
        }

        var summaries = await topics.GetByIdsAsync(items.Select(item => item.TopicId).Distinct(), cancellationToken);
        IReadOnlyList<AssignedCouncilSimilarityReportDto> result = items.Select(item =>
            new AssignedCouncilSimilarityReportDto(item.GroupId, item.ReportId, item.VersionNo,
                summaries.TryGetValue(item.TopicId, out var topic) ? topic.TitleVi : "Đề tài chưa có tên"))
            .OrderBy(item => item.TopicTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FinalReportId)
            .ToList();
        return Result.Success(result);
    }
}
