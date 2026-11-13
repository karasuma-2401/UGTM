using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.AcademicResults.Common;

public sealed record AcademicResultComputedRow(
    AcademicResultListItemDto ListItem,
    AcademicResultDetailDto Detail,
    IReadOnlyList<AcademicResultExportRowDto> ExportRows);

public sealed class AcademicResultsWorkspaceLoader(
    IAcademicResultsThesisReader thesisReader,
    ITopicDefenseContract topicContract,
    ICouncilUserInfoReader userInfoReader,
    IStudentSearchReader studentSearchReader,
    ICouncilRepository councilRepository,
    IScoreSummaryStore scoreSummaryStore,
    IFinalSubmissionArchiveProofContract archiveProofContract)
{
    private const string DefaultFacultyName = "Khoa CNTT";

    public async Task<IReadOnlyList<AcademicResultComputedRow>> LoadAllAsync(
        Guid batchId,
        string phase,
        string? keyword = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var normalizedPhase = NormalizePhase(phase);
        var groups = await thesisReader.GetGroupsByBatchIdAsync(batchId, ct);
        if (groups.Count == 0)
        {
            return [];
        }

        var allGroupIds = groups.Select(g => g.GroupId).Distinct().ToList();
        var allTopicIds = groups.Select(g => g.TopicId).Distinct().ToList();
        var topics = allTopicIds.Count > 0
            ? await topicContract.GetTopicsDetailsAsync(allTopicIds, ct)
            : new Dictionary<Guid, TopicDefenseDetailsContractDto>();

        var allStudentIds = groups.SelectMany(g => g.Members).Select(m => m.StudentId).Distinct().ToList();
        var allAdvisorIds = topics.Values.Select(t => t.AdvisorId).Where(id => id != Guid.Empty).Distinct().ToList();

        // Nạp Councils và Assignments để map OrderNumber (mã nhóm N01, N02...)
        var councils = await councilRepository.GetCouncilsForOverviewAsync(batchId, null, ct);
        var assignmentsByGroup = councils
            .SelectMany(c => c.Assignments.Select(a => (Council: c, Assignment: a)))
            .GroupBy(x => x.Assignment.GroupId)
            .ToDictionary(g => g.Key, g => g.First());

        // Đọc dữ liệu điểm & lưu chiểu nếu pha là Final
        IReadOnlyDictionary<Guid, ScoreFacts> scoreFacts = new Dictionary<Guid, ScoreFacts>();
        IReadOnlyDictionary<Guid, ScoreHistory> scoreHistories = new Dictionary<Guid, ScoreHistory>();
        IReadOnlyDictionary<Guid, FinalSubmissionArchiveProofContractDto> archiveProofs =
            new Dictionary<Guid, FinalSubmissionArchiveProofContractDto>();

        var activeReviewerIds = allGroupIds.Count > 0
            ? await councilRepository.GetActiveReviewerIdsByGroupIdsAsync(allGroupIds, ct)
            : new Dictionary<Guid, Guid>();

        if (normalizedPhase == "Final")
        {
            scoreFacts = await scoreSummaryStore.FactsAsync(allGroupIds, false, ct);
            scoreHistories = await scoreSummaryStore.HistoriesAsync(allGroupIds, ct);
            var proofs = await archiveProofContract.GetManyArchiveProofsAsync(allGroupIds, ct);
            archiveProofs = proofs.ToDictionary(p => p.GroupId);
        }

        // Thu thập userIds cần đọc tên & mã
        var userIdsToFetch = new HashSet<Guid>(allStudentIds);
        foreach (var advId in allAdvisorIds)
        {
            userIdsToFetch.Add(advId);
        }
        foreach (var revId in activeReviewerIds.Values)
        {
            if (revId != Guid.Empty)
            {
                userIdsToFetch.Add(revId);
            }
        }
        foreach (var c in councils)
        {
            foreach (var m in c.Members)
            {
                userIdsToFetch.Add(m.UserId);
            }
        }

        var usersDict = userIdsToFetch.Count > 0
            ? await userInfoReader.GetUsersByIdsAsync(userIdsToFetch, ct)
            : new Dictionary<Guid, CouncilUserInfoContractDto>();

        // Tìm kiếm theo từ khóa nếu có
        HashSet<Guid>? studentIdsMatchingKeyword = null;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var matchedIds = await studentSearchReader.FindIdsByNameOrCodeAsync(keyword.Trim(), ct);
            studentIdsMatchingKeyword = matchedIds.ToHashSet();
        }

        var resultRows = new List<AcademicResultComputedRow>();
        var sortedGroups = groups.OrderBy(g => g.CreatedAtUtc).ThenBy(g => g.GroupId).ToList();

        for (var i = 0; i < sortedGroups.Count; i++)
        {
            var group = sortedGroups[i];
            assignmentsByGroup.TryGetValue(group.GroupId, out var councilAssign);
            var assignment = councilAssign.Assignment;
            var council = councilAssign.Council;

            // Xác định groupCode
            var groupCode = assignment != null
                ? $"N{assignment.OrderNumber:D2}"
                : $"N{i + 1:D2}";

            topics.TryGetValue(group.TopicId, out var topic);
            var topicTitleVi = topic?.TitleVi ?? "Chưa có tên đề tài";
            var topicTitleEn = topic?.TitleEn;
            var advisorId = topic?.AdvisorId ?? Guid.Empty;
            usersDict.TryGetValue(advisorId, out var advisorUser);
            var advisorName = advisorUser?.FullName ?? "Chưa phân công";

            // Danh sách sinh viên
            var students = new List<AcademicResultStudentDto>();
            foreach (var member in group.Members)
            {
                usersDict.TryGetValue(member.StudentId, out var stuUser);
                students.Add(new AcademicResultStudentDto(
                    member.StudentId,
                    stuUser?.UserCode ?? "",
                    stuUser?.FullName ?? "Sinh viên",
                    stuUser?.Email ?? ""));
            }

            var studentNames = students.Select(s => s.FullName).ToList();
            var studentCodes = students.Select(s => s.StudentCode).Where(c => !string.IsNullOrEmpty(c)).ToList();

            // Tính toán theo Phase
            AcademicResultComputedRow row;
            if (normalizedPhase == "Midterm")
            {
                row = BuildMidtermRow(
                    group,
                    groupCode,
                    batchId,
                    topic,
                    topicTitleVi,
                    topicTitleEn,
                    advisorId,
                    advisorName,
                    students,
                    studentNames,
                    studentCodes,
                    usersDict);
            }
            else
            {
                scoreFacts.TryGetValue(group.GroupId, out var facts);
                scoreHistories.TryGetValue(group.GroupId, out var history);
                archiveProofs.TryGetValue(group.GroupId, out var archiveProof);
                activeReviewerIds.TryGetValue(group.GroupId, out var reviewerId);

                row = BuildFinalRow(
                    group,
                    groupCode,
                    batchId,
                    topic,
                    topicTitleVi,
                    topicTitleEn,
                    advisorId,
                    advisorName,
                    reviewerId,
                    council,
                    assignment,
                    facts,
                    history,
                    archiveProof,
                    students,
                    studentNames,
                    studentCodes,
                    usersDict);
            }

            // Lọc theo keyword
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                var matchesCode = row.ListItem.GroupCode.Contains(kw, StringComparison.OrdinalIgnoreCase);
                var matchesTitleVi = row.ListItem.TopicTitleVi.Contains(kw, StringComparison.OrdinalIgnoreCase);
                var matchesTitleEn = row.ListItem.TopicTitleEn != null && row.ListItem.TopicTitleEn.Contains(kw, StringComparison.OrdinalIgnoreCase);
                var matchesStuName = row.ListItem.StudentNames.Any(n => n.Contains(kw, StringComparison.OrdinalIgnoreCase));
                var matchesStuCode = row.ListItem.StudentCodes.Any(c => c.Contains(kw, StringComparison.OrdinalIgnoreCase));
                var matchesStudentSearch = studentIdsMatchingKeyword != null && group.Members.Any(m => studentIdsMatchingKeyword.Contains(m.StudentId));

                if (!matchesCode && !matchesTitleVi && !matchesTitleEn && !matchesStuName && !matchesStuCode && !matchesStudentSearch)
                {
                    continue;
                }
            }

            // Lọc theo status
            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                if (!row.ListItem.StatusCode.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            resultRows.Add(row);
        }

        return resultRows;
    }

    public async Task<AcademicResultDetailDto?> LoadDetailAsync(
        Guid groupId,
        string phase,
        CancellationToken ct = default)
    {
        var normalizedPhase = NormalizePhase(phase);
        var group = await thesisReader.GetGroupByIdAsync(groupId, ct);
        if (group == null)
        {
            return null;
        }

        var rows = await LoadAllAsync(group.BatchId, normalizedPhase, null, null, ct);
        return rows.FirstOrDefault(r => r.ListItem.GroupId == groupId)?.Detail;
    }

    private static AcademicResultComputedRow BuildMidtermRow(
        AcademicResultGroupSnapshotContractDto group,
        string groupCode,
        Guid batchId,
        TopicDefenseDetailsContractDto? topic,
        string topicTitleVi,
        string? topicTitleEn,
        Guid advisorId,
        string advisorName,
        IReadOnlyList<AcademicResultStudentDto> students,
        IReadOnlyList<string> studentNames,
        IReadOnlyList<string> studentCodes,
        IReadOnlyDictionary<Guid, CouncilUserInfoContractDto> usersDict)
    {
        var midterm = group.MidtermReport;
        string statusCode;
        string statusLabel;
        string detailLabel;

        if (group.GroupStatus.Equals("Stopped", StringComparison.OrdinalIgnoreCase) ||
            (midterm != null && midterm.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase)))
        {
            statusCode = "STOPPED";
            statusLabel = "Dừng thực hiện";
            detailLabel = "Không đạt giữa kỳ";
        }
        else if (midterm != null && midterm.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            statusCode = "CONTINUED";
            statusLabel = "Tiếp tục";
            detailLabel = "Đã duyệt giữa kỳ";
        }
        else
        {
            statusCode = "PENDING_MIDTERM";
            statusLabel = "Chờ xét duyệt";
            if (midterm != null && midterm.Status.Equals("Submitted", StringComparison.OrdinalIgnoreCase))
            {
                detailLabel = "Chờ Khoa duyệt";
            }
            else if (midterm != null && midterm.Status.Equals("NeedRevision", StringComparison.OrdinalIgnoreCase))
            {
                detailLabel = "Cần chỉnh sửa";
            }
            else
            {
                detailLabel = "Chưa nộp báo cáo";
            }
        }

        AcademicResultMidtermDetailDto? midtermDetail = null;
        if (midterm != null)
        {
            string? reviewerName = null;
            if (midterm.ReviewedBy.HasValue && usersDict.TryGetValue(midterm.ReviewedBy.Value, out var revUser))
            {
                reviewerName = revUser.FullName;
            }

            var midLabel = midterm.Status switch
            {
                "Approved" => "Đã duyệt",
                "Rejected" => "Không đạt",
                "NeedRevision" => "Cần chỉnh sửa",
                _ => "Đã nộp"
            };

            midtermDetail = new AcademicResultMidtermDetailDto(
                midterm.Status,
                midLabel,
                midterm.CurrentVersionNo,
                midterm.FacultyFeedback,
                midterm.ReviewedBy,
                reviewerName,
                midterm.ReviewedAtUtc);
        }

        var listItem = new AcademicResultListItemDto(
            group.GroupId,
            groupCode,
            group.TopicId,
            topicTitleVi,
            topicTitleEn,
            studentNames,
            studentCodes,
            DefaultFacultyName,
            "Midterm",
            statusCode,
            statusLabel,
            detailLabel,
            FinalScore: null);

        var detail = new AcademicResultDetailDto(
            group.GroupId,
            groupCode,
            batchId,
            "Midterm",
            DefaultFacultyName,
            group.TopicId,
            null,
            topicTitleVi,
            topicTitleEn,
            advisorId,
            advisorName,
            students,
            statusCode,
            statusLabel,
            detailLabel,
            midtermDetail,
            ScoreSources: [],
            AdvisorScore: null,
            ReviewerScore: null,
            CouncilScore: null,
            FinalScore: null,
            FormulaSnapshot: null,
            ArchiveStatus: "-",
            ArchiveStatusLabel: "-",
            PublicationStatus: "-",
            PublicationStatusLabel: "-",
            PublishedAtUtc: null);

        var exportRows = students.Count > 0
            ? students.Select(s => new AcademicResultExportRowDto(
                s.StudentCode,
                s.FullName,
                groupCode,
                topicTitleVi,
                DefaultFacultyName,
                AdvisorScoreText: "",
                ReviewerScoreText: "",
                CouncilScoreText: "",
                FinalScoreText: "",
                ArchiveStatusLabel: detailLabel,
                PublicationStatusLabel: statusLabel)).ToList()
            : [new AcademicResultExportRowDto(
                "",
                "",
                groupCode,
                topicTitleVi,
                DefaultFacultyName,
                AdvisorScoreText: "",
                ReviewerScoreText: "",
                CouncilScoreText: "",
                FinalScoreText: "",
                ArchiveStatusLabel: detailLabel,
                PublicationStatusLabel: statusLabel)];

        return new AcademicResultComputedRow(listItem, detail, exportRows);
    }

    private static AcademicResultComputedRow BuildFinalRow(
        AcademicResultGroupSnapshotContractDto group,
        string groupCode,
        Guid batchId,
        TopicDefenseDetailsContractDto? topic,
        string topicTitleVi,
        string? topicTitleEn,
        Guid advisorId,
        string advisorName,
        Guid reviewerId,
        Council? council,
        CouncilAssignment? assignment,
        ScoreFacts? facts,
        ScoreHistory? history,
        FinalSubmissionArchiveProofContractDto? archiveProof,
        IReadOnlyList<AcademicResultStudentDto> students,
        IReadOnlyList<string> studentNames,
        IReadOnlyList<string> studentCodes,
        IReadOnlyDictionary<Guid, CouncilUserInfoContractDto> usersDict)
    {
        // 1. Lưu chiểu
        var isArchiveConfirmed = archiveProof?.Outcome == ArchiveProofOutcome.Succeeded
            || string.Equals(group.FinalSubmissionStatus, "ArchiveConfirmed", StringComparison.OrdinalIgnoreCase);

        var archiveStatus = isArchiveConfirmed ? "ArchiveConfirmed" : (group.FinalSubmissionStatus ?? "NotSubmitted");
        var archiveStatusLabel = isArchiveConfirmed ? "Lưu chiểu đạt" : "Chưa đủ lưu chiểu";

        // 2. Điểm thành phần
        var advisorEval = facts?.Advisors.FirstOrDefault(a => !a.IsIgnoredInCalculation);
        decimal? advisorScore = advisorEval?.Score;

        var reviewerEval = facts?.Evaluations.FirstOrDefault(r => !r.IsIgnoredInCalculation && r.Score.HasValue);
        decimal? reviewerScore = reviewerEval?.Score;

        var validCouncilScores = facts?.Scores
            .Where(s => !s.IsIgnoredInCalculation)
            .Select(s => s.Score)
            .ToList() ?? [];

        decimal? councilScore = validCouncilScores.Count > 0
            ? Math.Round(validCouncilScores.Average(), 2)
            : null;

        var minute = facts?.Minutes.FirstOrDefault();
        var isMinuteLocked = minute?.LockStatus == CouncilMinuteLockStatus.Locked;

        // Điểm tổng kết
        decimal? finalScore = history?.Summary.FinalScore;

        var isPublished = history?.Summary.Status == ScoreSummaryStatus.PublishedToStudent;
        var hasFullScores = (history?.Summary.Status is ScoreSummaryStatus.Calculated or ScoreSummaryStatus.PublishedToStudent)
            || (advisorScore.HasValue && reviewerScore.HasValue && validCouncilScores.Count > 0 && isMinuteLocked);

        string statusCode;
        string statusLabel;
        string detailLabel;

        if (isPublished)
        {
            statusCode = "PUBLISHED";
            statusLabel = "Khoa đã công bố";
            detailLabel = archiveStatusLabel;
        }
        else if (hasFullScores && isArchiveConfirmed)
        {
            statusCode = "READY_TO_PUBLISH";
            statusLabel = "Chờ công bố";
            detailLabel = "Lưu chiểu đạt";
        }
        else if (hasFullScores && !isArchiveConfirmed)
        {
            statusCode = "MISSING_ARCHIVE";
            statusLabel = "Chưa đủ lưu chiểu";
            detailLabel = "Chưa công bố";
        }
        else
        {
            statusCode = "MISSING_SCORE";
            statusLabel = "Chưa đủ điểm";
            detailLabel = isArchiveConfirmed ? "Lưu chiểu đạt" : "Chưa công bố";
        }

        var publicationStatus = isPublished ? "PublishedToStudent" : (history?.Summary.Status.ToString() ?? "Pending");
        var publicationStatusLabel = isPublished
            ? "Khoa đã công bố"
            : (hasFullScores && isArchiveConfirmed ? "Chờ công bố" : "Chưa công bố");

        // Xây dựng danh sách nguồn điểm (ScoreSources)
        var scoreSources = new List<AcademicResultScoreSourceDto>();

        // Nguồn GVHD
        if (advisorId != Guid.Empty || advisorEval != null)
        {
            scoreSources.Add(new AcademicResultScoreSourceDto(
                "ADVISOR",
                "Giảng viên hướng dẫn",
                advisorId,
                advisorName,
                advisorScore,
                0.20m,
                advisorEval?.IsIgnoredInCalculation ?? false,
                advisorEval != null ? "Submitted" : "Pending",
                advisorEval?.Comment,
                advisorEval?.EvaluatedAt));
        }

        // Nguồn GVPB
        if (reviewerId != Guid.Empty || reviewerEval != null)
        {
            usersDict.TryGetValue(reviewerId, out var revUser);
            var reviewerName = revUser?.FullName ?? "Giảng viên phản biện";
            scoreSources.Add(new AcademicResultScoreSourceDto(
                "REVIEWER",
                "Giảng viên phản biện",
                reviewerId,
                reviewerName,
                reviewerScore,
                0.20m,
                reviewerEval?.IsIgnoredInCalculation ?? false,
                reviewerEval?.Status.ToString() ?? "Pending",
                reviewerEval?.Comment,
                reviewerEval?.SubmittedAtUtc));
        }

        // Nguồn các thành viên Hội đồng
        if (council != null)
        {
            foreach (var member in council.Members)
            {
                var memberScore = facts?.Scores.FirstOrDefault(s => s.CouncilMemberId == member.Id);
                usersDict.TryGetValue(member.UserId, out var memberUser);
                var memberName = memberUser?.FullName ?? "Thành viên hội đồng";

                var (srcCode, srcLabel) = member.RoleInCouncil switch
                {
                    CouncilMemberRole.Chairman => ("COUNCIL_CHAIRMAN", "Chủ tịch Hội đồng"),
                    CouncilMemberRole.Secretary => ("COUNCIL_SECRETARY", "Thư ký Hội đồng"),
                    _ => ("COUNCIL_MEMBER", "Ủy viên Hội đồng")
                };

                scoreSources.Add(new AcademicResultScoreSourceDto(
                    srcCode,
                    srcLabel,
                    member.UserId,
                    memberName,
                    memberScore?.Score,
                    0.20m,
                    memberScore?.IsIgnoredInCalculation ?? false,
                    memberScore != null ? "Submitted" : "Pending",
                    memberScore?.Feedback,
                    memberScore?.EvaluatedAt));
            }
        }

        // Thông tin giữa kỳ bổ trợ
        AcademicResultMidtermDetailDto? midtermDetail = null;
        if (group.MidtermReport != null)
        {
            string? revName = null;
            if (group.MidtermReport.ReviewedBy.HasValue && usersDict.TryGetValue(group.MidtermReport.ReviewedBy.Value, out var u))
            {
                revName = u.FullName;
            }

            var midLabel = group.MidtermReport.Status switch
            {
                "Approved" => "Đã duyệt",
                "Rejected" => "Không đạt",
                "NeedRevision" => "Cần chỉnh sửa",
                _ => "Đã nộp"
            };

            midtermDetail = new AcademicResultMidtermDetailDto(
                group.MidtermReport.Status,
                midLabel,
                group.MidtermReport.CurrentVersionNo,
                group.MidtermReport.FacultyFeedback,
                group.MidtermReport.ReviewedBy,
                revName,
                group.MidtermReport.ReviewedAtUtc);
        }

        var listItem = new AcademicResultListItemDto(
            group.GroupId,
            groupCode,
            group.TopicId,
            topicTitleVi,
            topicTitleEn,
            studentNames,
            studentCodes,
            DefaultFacultyName,
            "Final",
            statusCode,
            statusLabel,
            detailLabel,
            finalScore);

        var detail = new AcademicResultDetailDto(
            group.GroupId,
            groupCode,
            batchId,
            "Final",
            DefaultFacultyName,
            group.TopicId,
            null,
            topicTitleVi,
            topicTitleEn,
            advisorId,
            advisorName,
            students,
            statusCode,
            statusLabel,
            detailLabel,
            midtermDetail,
            scoreSources,
            advisorScore,
            reviewerScore,
            councilScore,
            finalScore,
            FormulaSnapshot: "20% GVHD + 20% GVPB + 60% Hội đồng",
            archiveStatus,
            archiveStatusLabel,
            publicationStatus,
            publicationStatusLabel,
            history?.Summary.PublishedAt);

        var advisorScoreStr = advisorScore.HasValue ? advisorScore.Value.ToString("F2") : "";
        var reviewerScoreStr = reviewerScore.HasValue ? reviewerScore.Value.ToString("F2") : "";
        var councilScoreStr = councilScore.HasValue ? councilScore.Value.ToString("F2") : "";
        var finalScoreStr = finalScore.HasValue ? finalScore.Value.ToString("F2") : "";

        var exportRows = students.Count > 0
            ? students.Select(s => new AcademicResultExportRowDto(
                s.StudentCode,
                s.FullName,
                groupCode,
                topicTitleVi,
                DefaultFacultyName,
                advisorScoreStr,
                reviewerScoreStr,
                councilScoreStr,
                finalScoreStr,
                archiveStatusLabel,
                statusLabel)).ToList()
            : [new AcademicResultExportRowDto(
                "",
                "",
                groupCode,
                topicTitleVi,
                DefaultFacultyName,
                advisorScoreStr,
                reviewerScoreStr,
                councilScoreStr,
                finalScoreStr,
                archiveStatusLabel,
                statusLabel)];

        return new AcademicResultComputedRow(listItem, detail, exportRows);
    }

    public static string NormalizePhase(string phase)
    {
        if (string.Equals(phase, "Midterm", StringComparison.OrdinalIgnoreCase))
        {
            return "Midterm";
        }
        if (string.Equals(phase, "Final", StringComparison.OrdinalIgnoreCase))
        {
            return "Final";
        }
        return phase;
    }
}
