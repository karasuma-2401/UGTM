using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilsOverview;

public sealed class GetCouncilsOverviewHandler(
    ICouncilRepository councilRepository,
    IDefenseGroupContract defenseGroupContract,
    ITopicDefenseContract topicDefenseContract,
    ICouncilUserInfoReader userInfoReader,
    ICurrentUser currentUser)
{
    private const string DefaultFacultyName = "Khoa CNTT";

    public async Task<Result<IReadOnlyList<CouncilOverviewItemDto>>> HandleAsync(
        GetCouncilsOverviewQuery query,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure<IReadOnlyList<CouncilOverviewItemDto>>(CouncilErrors.Forbidden);
        }

        var isAuthorized = currentUser.Roles.Any(r => r is "Faculty" or "AcademicAffairs" or "Admin");
        if (!isAuthorized)
        {
            return Result.Failure<IReadOnlyList<CouncilOverviewItemDto>>(CouncilErrors.Forbidden);
        }

        CouncilStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<CouncilStatus>(query.Status, true, out var statusVal))
            {
                parsedStatus = statusVal;
            }
            else
            {
                // Trạng thái không hợp lệ trong hệ thống -> trả về danh sách rỗng
                return Result.Success<IReadOnlyList<CouncilOverviewItemDto>>([]);
            }
        }

        // 1. Nạp danh sách hội đồng
        var councils = await councilRepository.GetCouncilsForOverviewAsync(query.BatchId, parsedStatus, ct);
        if (councils.Count == 0)
        {
            return Result.Success<IReadOnlyList<CouncilOverviewItemDto>>([]);
        }

        // 2. Thu thập IDs để nạp thông tin đợt / nhóm / đề tài / giảng viên
        var allAssignments = councils.SelectMany(c => c.Assignments).ToList();
        var allGroupIds = allAssignments.Select(a => a.GroupId).Distinct().ToList();
        var allMemberUserIds = councils.SelectMany(c => c.Members).Select(m => m.UserId).Distinct().ToList();

        var groups = allGroupIds.Count > 0
            ? await defenseGroupContract.GetGroupsByIdsAsync(allGroupIds, ct)
            : new Dictionary<Guid, DefenseGroupSnapshotContractDto>();

        var allTopicIds = groups.Values.Select(g => g.TopicId).Distinct().ToList();
        var topics = allTopicIds.Count > 0
            ? await topicDefenseContract.GetTopicsDetailsAsync(allTopicIds, ct)
            : new Dictionary<Guid, TopicDefenseDetailsContractDto>();

        var activeReviewers = allGroupIds.Count > 0
            ? await councilRepository.GetActiveReviewerIdsByGroupIdsAsync(allGroupIds, ct)
            : new Dictionary<Guid, Guid>();

        var userIdsToFetch = new HashSet<Guid>(allMemberUserIds);
        foreach (var topic in topics.Values)
        {
            if (topic.AdvisorId != Guid.Empty)
            {
                userIdsToFetch.Add(topic.AdvisorId);
            }
        }
        foreach (var revId in activeReviewers.Values)
        {
            if (revId != Guid.Empty)
            {
                userIdsToFetch.Add(revId);
            }
        }

        var userDict = userIdsToFetch.Count > 0
            ? await userInfoReader.GetUsersByIdsAsync(userIdsToFetch, ct)
            : new Dictionary<Guid, CouncilUserInfoContractDto>();

        // 3. Xây dựng danh sách phẳng (Council x Assignment)
        var resultList = new List<CouncilOverviewItemDto>();

        foreach (var council in councils)
        {
            var statusLabel = council.Status switch
            {
                CouncilStatus.Draft => "Chờ phê duyệt",
                CouncilStatus.Active => "Đã phê duyệt",
                CouncilStatus.Completed => "Đã hoàn thành",
                _ => council.Status.ToString()
            };

            var chairmanMember = council.Members.FirstOrDefault(m => m.RoleInCouncil == CouncilMemberRole.Chairman);
            var secretaryMember = council.Members.FirstOrDefault(m => m.RoleInCouncil == CouncilMemberRole.Secretary);
            var memberList = council.Members.Where(m => m.RoleInCouncil == CouncilMemberRole.Member).ToList();

            if (council.Assignments.Count == 0)
            {
                // Hội đồng dự thảo chưa gán nhóm đề tài nào
                var chairmanDto = chairmanMember != null
                    ? new CouncilOverviewMemberDto(
                        chairmanMember.UserId,
                        userDict.TryGetValue(chairmanMember.UserId, out var cmU) ? cmU.FullName : "Chủ tịch",
                        "Chairman",
                        IsReviewer: false,
                        IsAdvisorConflict: false,
                        Note: null)
                    : null;

                var secretaryDto = secretaryMember != null
                    ? new CouncilOverviewMemberDto(
                        secretaryMember.UserId,
                        userDict.TryGetValue(secretaryMember.UserId, out var secU) ? secU.FullName : "Thư ký",
                        "Secretary",
                        IsReviewer: false,
                        IsAdvisorConflict: false,
                        Note: null)
                    : null;

                var membersDto = memberList.Select(m => new CouncilOverviewMemberDto(
                    m.UserId,
                    userDict.TryGetValue(m.UserId, out var memU) ? memU.FullName : "Ủy viên",
                    "Member",
                    IsReviewer: false,
                    IsAdvisorConflict: false,
                    Note: "Ủy viên độc lập")).ToList();

                resultList.Add(new CouncilOverviewItemDto(
                    council.Id,
                    council.BatchId,
                    council.CouncilCode,
                    DefaultFacultyName,
                    GroupCode: null,
                    TopicTitle: null,
                    council.Room,
                    council.TimeSlot,
                    council.DefenseDate,
                    council.Status.ToString(),
                    statusLabel,
                    RosterModel: "IndependentMember",
                    RosterModelLabel: "CT · TK · UV độc lập",
                    ProfileStatus: "Pending",
                    ProfileStatusLabel: "Chưa có phiếu",
                    MinuteLockStatus: "Open",
                    MinuteLockStatusLabel: "Chưa khóa",
                    AssignmentId: null,
                    chairmanDto,
                    secretaryDto,
                    membersDto));
            }
            else
            {
                foreach (var assignment in council.Assignments.OrderBy(a => a.OrderNumber))
                {
                    groups.TryGetValue(assignment.GroupId, out var groupSnapshot);
                    var groupCode = $"N{assignment.OrderNumber:D2}";
                    topics.TryGetValue(groupSnapshot?.TopicId ?? Guid.Empty, out var topic);
                    var topicTitle = topic?.TitleVi;
                    var advisorId = topic?.AdvisorId ?? Guid.Empty;

                    activeReviewers.TryGetValue(assignment.GroupId, out var reviewerId);
                    var isReviewerInCouncil = reviewerId != Guid.Empty && council.Members.Any(m => m.UserId == reviewerId);

                    var rosterModel = isReviewerInCouncil ? "ReviewerInCouncil" : "IndependentMember";
                    var rosterModelLabel = isReviewerInCouncil ? "CT · TK · GVPB kiêm UV" : "CT · TK · UV độc lập";

                    // Trạng thái khóa biên bản
                    var minute = assignment.Minute;
                    var minuteLockStatus = minute != null ? minute.LockStatus.ToString() : "Open";
                    var minuteLockStatusLabel = minute?.LockStatus switch
                    {
                        CouncilMinuteLockStatus.Locked => "Đã khóa",
                        CouncilMinuteLockStatus.UnlockedByOverride => "Mở khóa đặc biệt",
                        CouncilMinuteLockStatus.AllScoresEntered => "Đã đủ điểm",
                        _ => "Chưa khóa"
                    };

                    // Trạng thái hồ sơ chấm điểm (ProfileStatus)
                    string profileStatus;
                    string profileStatusLabel;

                    if (minute?.LockStatus == CouncilMinuteLockStatus.Locked)
                    {
                        profileStatus = "Verified";
                        profileStatusLabel = "Đã xác thực";
                    }
                    else
                    {
                        var eligibleMemberIds = council.Members
                            .Where(m => (advisorId == Guid.Empty || m.UserId != advisorId)
                                        && (!isReviewerInCouncil || m.UserId != reviewerId))
                            .Select(m => m.Id)
                            .ToList();

                        var scoredMemberCount = assignment.Scores.Count(s => eligibleMemberIds.Contains(s.CouncilMemberId));
                        var totalEligibleCount = eligibleMemberIds.Count;

                        if (totalEligibleCount > 0 && scoredMemberCount >= totalEligibleCount)
                        {
                            profileStatus = "AllScoresSubmitted";
                            profileStatusLabel = "Đủ phiếu áp dụng";
                        }
                        else if (scoredMemberCount > 0 || assignment.Scores.Count > 0)
                        {
                            profileStatus = "PartiallySubmitted";
                            profileStatusLabel = "Đang chấm";
                        }
                        else
                        {
                            profileStatus = "Pending";
                            profileStatusLabel = "Chưa có phiếu";
                        }
                    }

                    // Thành viên & xung đột
                    var chairmanConflict = chairmanMember != null && advisorId != Guid.Empty && chairmanMember.UserId == advisorId;
                    var chairmanDto = chairmanMember != null
                        ? new CouncilOverviewMemberDto(
                            chairmanMember.UserId,
                            userDict.TryGetValue(chairmanMember.UserId, out var cmU) ? cmU.FullName : "Chủ tịch",
                            "Chairman",
                            IsReviewer: reviewerId != Guid.Empty && chairmanMember.UserId == reviewerId,
                            IsAdvisorConflict: chairmanConflict,
                            Note: null)
                        : null;

                    var secretaryConflict = secretaryMember != null && advisorId != Guid.Empty && secretaryMember.UserId == advisorId;
                    var secretaryDto = secretaryMember != null
                        ? new CouncilOverviewMemberDto(
                            secretaryMember.UserId,
                            userDict.TryGetValue(secretaryMember.UserId, out var secU) ? secU.FullName : "Thư ký",
                            "Secretary",
                            IsReviewer: reviewerId != Guid.Empty && secretaryMember.UserId == reviewerId,
                            IsAdvisorConflict: secretaryConflict,
                            Note: null)
                        : null;

                    var membersDto = memberList.Select(m =>
                    {
                        var isRev = reviewerId != Guid.Empty && m.UserId == reviewerId;
                        var isAdv = advisorId != Guid.Empty && m.UserId == advisorId;
                        var note = isRev ? "Chỉ tham dự, không chấm UV" : "Ủy viên độc lập";

                        return new CouncilOverviewMemberDto(
                            m.UserId,
                            userDict.TryGetValue(m.UserId, out var memU) ? memU.FullName : "Ủy viên",
                            "Member",
                            IsReviewer: isRev,
                            IsAdvisorConflict: isAdv,
                            Note: note);
                    }).ToList();

                    resultList.Add(new CouncilOverviewItemDto(
                        council.Id,
                        council.BatchId,
                        council.CouncilCode,
                        DefaultFacultyName,
                        groupCode,
                        topicTitle,
                        council.Room,
                        council.TimeSlot,
                        council.DefenseDate,
                        council.Status.ToString(),
                        statusLabel,
                        rosterModel,
                        rosterModelLabel,
                        profileStatus,
                        profileStatusLabel,
                        minuteLockStatus,
                        minuteLockStatusLabel,
                        assignment.Id,
                        chairmanDto,
                        secretaryDto,
                        membersDto));
                }
            }
        }

        // 4. Lọc theo LockStatus (nếu có)
        IEnumerable<CouncilOverviewItemDto> filtered = resultList;
        if (!string.IsNullOrWhiteSpace(query.LockStatus))
        {
            var targetLock = query.LockStatus.Trim();
            filtered = filtered.Where(x => string.Equals(x.MinuteLockStatus, targetLock, StringComparison.OrdinalIgnoreCase));
        }

        // 5. Lọc theo Keyword (nếu có)
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            filtered = filtered.Where(x =>
                (x.CouncilCode != null && x.CouncilCode.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || (x.Room != null && x.Room.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || (x.TopicTitle != null && x.TopicTitle.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || (x.GroupCode != null && x.GroupCode.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || (x.Chairman != null && x.Chairman.FullName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || (x.Secretary != null && x.Secretary.FullName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || x.Members.Any(m => m.FullName.Contains(kw, StringComparison.OrdinalIgnoreCase)));
        }

        // 6. Phân trang
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var pagedItems = filtered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result.Success<IReadOnlyList<CouncilOverviewItemDto>>(pagedItems);
    }
}
