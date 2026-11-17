using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Contracts.Councils;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;

public sealed record AssignedCouncilStudentDto(Guid Id, string FullName, string? UserCode);

public sealed record AssignedCouncilSimilarityReportDto(
    Guid GroupId,
    Guid FinalReportId,
    int VersionNo,
    string TopicTitle,
    string? TopicTitleEn = null,
    Guid? BatchId = null,
    IReadOnlyList<AssignedCouncilStudentDto>? Students = null,
    DateTime? SubmittedAt = null,
    string? ScanStatus = null,
    double? SimilarityCoverage = null,
    double? MaxRerankerScore = null,
    int CitedFindings = 0,
    int UncitedFindings = 0,
    int SuspectedFindings = 0,
    int TotalFindings = 0,
    DateTime? ScanCompletedAt = null);

public sealed class GetMyCouncilSimilarityReportsHandler(
    ICurrentUser currentUser,
    ICouncilAssignedGroupReader assignedGroups,
    IStudentGroupRepository groups,
    IFinalReportRepository reports,
    ITopicSummaryReader topics,
    IFinalReportPlagiarismScanRepository? scans = null,
    IStudentInfoReader? studentInfoReader = null)
{
    public async Task<Result<IReadOnlyList<AssignedCouncilSimilarityReportDto>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty
            || !currentUser.Roles.Any(role => string.Equals(role, "CouncilMember", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<IReadOnlyList<AssignedCouncilSimilarityReportDto>>(PlagiarismScanErrors.AccessDenied);
        }

        var assigned = await assignedGroups.GetAssignedGroupIdsAsync(currentUser.UserId.Value, cancellationToken);
        var items = new List<(
            Guid GroupId,
            Guid TopicId,
            IReadOnlyList<Guid> StudentIds,
            FinalReport Report,
            FinalReportPlagiarismScan? Scan)>();

        foreach (var groupId in assigned.Distinct())
        {
            var group = await groups.GetByIdAsync(groupId, cancellationToken);
            if (group is null) continue;

            var studentIds = group.Members
                .Where(m => m.ConfirmationStatus == MemberConfirmationStatus.Confirmed)
                .OrderBy(m => m.JoinedAt)
                .Select(m => m.StudentId)
                .Distinct()
                .ToList();

            foreach (var kind in Enum.GetValues<FinalReportVersionType>())
            {
                var report = await reports.GetByGroupAndVersionTypeAsync(groupId, kind, cancellationToken);
                if (report?.Status != FinalReportStatus.UnderEvaluation) continue;

                FinalReportPlagiarismScan? scan = null;
                if (scans is not null)
                {
                    scan = await scans.GetByReportAndVersionAsync(report.Id, report.CurrentVersionNo, cancellationToken)
                        ?? await scans.GetLatestByReportIdAsync(report.Id, cancellationToken);
                }

                items.Add((groupId, group.TopicId, studentIds, report, scan));
            }
        }

        var summaries = await topics.GetByIdsAsync(items.Select(item => item.TopicId).Distinct(), cancellationToken);
        var allStudentIds = items.SelectMany(item => item.StudentIds).Distinct().ToList();
        var studentMap = allStudentIds.Count > 0 && studentInfoReader is not null
            ? await studentInfoReader.GetStudentsByIdsAsync(allStudentIds, cancellationToken)
            : new Dictionary<Guid, StudentInfoContractDto>();

        IReadOnlyList<AssignedCouncilSimilarityReportDto> result = items.Select(item =>
            {
                summaries.TryGetValue(item.TopicId, out var topic);
                var students = item.StudentIds
                    .Select(id => studentMap.TryGetValue(id, out var info)
                        ? new AssignedCouncilStudentDto(id, info.FullName, info.UserCode)
                        : null)
                    .Where(s => s is not null)
                    .Cast<AssignedCouncilStudentDto>()
                    .ToList();

                var (cited, uncited, suspected, totalFindings, coverage, maxScore) = ResolveScanMetrics(item.Scan);

                return new AssignedCouncilSimilarityReportDto(
                    item.GroupId,
                    item.Report.Id,
                    item.Report.CurrentVersionNo,
                    topic?.TitleVi ?? "Đề tài chưa có tên",
                    topic?.TitleEn,
                    topic?.BatchId,
                    students,
                    item.Report.SubmittedAt,
                    item.Scan?.Status.ToString(),
                    coverage,
                    maxScore,
                    cited,
                    uncited,
                    suspected,
                    totalFindings,
                    item.Scan?.CompletedAt);
            })
            .OrderBy(item => item.TopicTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FinalReportId)
            .ToList();

        return Result.Success(result);
    }

    private static (
        int Cited,
        int Uncited,
        int Suspected,
        int TotalFindings,
        double? Coverage,
        double? MaxScore) ResolveScanMetrics(FinalReportPlagiarismScan? scan)
    {
        if (scan is null)
        {
            return (0, 0, 0, 0, null, null);
        }

        var cited = scan.CitedFindingsCount ?? 0;
        var uncited = scan.UncitedFindingsCount ?? 0;
        var suspected = scan.SuspectedFindingsCount ?? 0;
        var total = cited + uncited + suspected;
        var coverage = scan.SimilarityCoverage;
        var maxScore = scan.MaxRerankerScore;

        if (scan.Status == PlagiarismScanStatus.Completed && !string.IsNullOrWhiteSpace(scan.RawReportJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(scan.RawReportJson);
                var (summary, findings, _) = PlagiarismReportParser.Parse(doc.RootElement);
                if (summary is not null)
                {
                    coverage ??= summary.SimilarityCoverage;
                    maxScore ??= summary.MaxRerankerScore;
                    cited = summary.CitedFindings;
                    uncited = summary.UncitedFindings;
                    suspected = summary.SuspectedFindings ?? suspected;
                }
                if (findings.Count > 0)
                {
                    total = findings.Count;
                }
            }
            catch
            {
                // Keep scalar DB columns if RawReportJson cannot be parsed.
            }
        }

        return (cited, uncited, suspected, total, coverage, maxScore);
    }
}
