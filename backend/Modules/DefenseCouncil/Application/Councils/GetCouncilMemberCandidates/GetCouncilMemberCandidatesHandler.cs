using System.Security.Cryptography;
using System.Text;
using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMemberCandidates;

public sealed record GetCouncilMemberCandidatesQuery(
    Guid CouncilId,
    string? Keyword = null);

public sealed class GetCouncilMemberCandidatesHandler(
    ICouncilRepository councilRepository,
    ICouncilUserInfoReader userInfoReader,
    IDefenseGroupContract defenseGroupContract,
    ITopicDefenseContract topicDefenseContract,
    ICurrentUser currentUser,
    ICacheService? cache = null)
{
    private static readonly IReadOnlyList<string> FullRoles = ["Chairman", "Secretary", "Member"];
    private static readonly IReadOnlyList<string> ReviewerOnlyRoles = ["Member"];
    private static readonly IReadOnlyList<string> NoRoles = [];

    public async Task<Result<IReadOnlyList<CouncilMemberCandidateDto>>> HandleAsync(
        GetCouncilMemberCandidatesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!CouncilMemberAssignmentHelper.IsCouncilStaffOrAdmin(currentUser))
        {
            return Result.Failure<IReadOnlyList<CouncilMemberCandidateDto>>(CouncilErrors.Forbidden);
        }

        var council = await councilRepository.GetByIdWithDetailsAsync(query.CouncilId, cancellationToken);
        if (council is null)
        {
            return Result.Failure<IReadOnlyList<CouncilMemberCandidateDto>>(CouncilErrors.NotFound);
        }

        string? cacheKey = null;
        if (cache is not null)
        {
            var councilVer = await cache.GetVersionAsync(DefenseCouncilCacheKeys.CouncilScope(council.Id), cancellationToken);
            var batchVer = await cache.GetVersionAsync(DefenseCouncilCacheKeys.BatchScope(council.BatchId), cancellationToken);
            var globalVer = await cache.GetVersionAsync(DefenseCouncilCacheKeys.GlobalScope, cancellationToken);
            var keywordKey = ComputeKeywordKey(query.Keyword);
            cacheKey = DefenseCouncilCacheKeys.CouncilMemberCandidates(council.Id, keywordKey, councilVer, batchVer, globalVer);

            var cached = await cache.GetAsync<List<CouncilMemberCandidateDto>>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result.Success<IReadOnlyList<CouncilMemberCandidateDto>>(cached);
            }
        }

        var lecturers = await userInfoReader.GetActiveLecturersAsync(query.Keyword, cancellationToken);
        if (lecturers.Count == 0)
        {
            if (cache is not null && cacheKey is not null)
            {
                await cache.SetAsync(cacheKey, new List<CouncilMemberCandidateDto>(), DefenseCouncilCacheKeys.MemberCandidatesTtl, cancellationToken);
            }
            return Result.Success<IReadOnlyList<CouncilMemberCandidateDto>>([]);
        }

        var existingMemberMap = council.Members
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.First().RoleInCouncil.ToString());

        var rolesInfo = await CouncilMemberAssignmentHelper.ResolveAssignmentRolesAsync(
            council.Assignments.ToList(),
            councilRepository,
            defenseGroupContract,
            topicDefenseContract,
            cancellationToken);

        var conflictingUserIds = await councilRepository.GetConflictingMemberUserIdsAsync(
            council.DefenseDate,
            council.TimeSlot,
            lecturers.Select(l => l.Id),
            council.Id,
            cancellationToken);
        var conflictingSet = conflictingUserIds.ToHashSet();

        var result = new List<CouncilMemberCandidateDto>(lecturers.Count);
        foreach (var lecturer in lecturers)
        {
            var isAlreadyInCouncil = existingMemberMap.TryGetValue(lecturer.Id, out var currentRole);
            var isAdvisorConflict = rolesInfo.AdvisorTopicsByUserId.ContainsKey(lecturer.Id);
            var isScheduleConflict = conflictingSet.Contains(lecturer.Id);
            var isReviewer = rolesInfo.ReviewerTopicsByUserId.ContainsKey(lecturer.Id);
            var isEligible = !isAdvisorConflict && !isScheduleConflict;

            IReadOnlyList<string> allowedRoles;
            string? conflictReason;

            if (isAdvisorConflict)
            {
                allowedRoles = NoRoles;
                conflictReason = "Là GVHD của nhóm đề tài trong hội đồng";
            }
            else if (isScheduleConflict)
            {
                allowedRoles = NoRoles;
                conflictReason = "Trùng lịch tham gia hội đồng khác trong cùng ca bảo vệ";
            }
            else if (isReviewer)
            {
                allowedRoles = ReviewerOnlyRoles;
                conflictReason = "Là GVPB của nhóm đề tài trong hội đồng (chỉ được làm Ủy viên)";
            }
            else
            {
                allowedRoles = FullRoles;
                conflictReason = null;
            }

            result.Add(new CouncilMemberCandidateDto(
                UserId: lecturer.Id,
                FullName: lecturer.FullName,
                Email: lecturer.Email,
                UserCode: lecturer.UserCode,
                IsAlreadyInCouncil: isAlreadyInCouncil,
                CurrentRoleInCouncil: currentRole,
                IsScheduleConflict: isScheduleConflict,
                IsAdvisorConflict: isAdvisorConflict,
                IsReviewerForCouncilGroups: isReviewer,
                IsEligible: isEligible,
                AllowedRoles: allowedRoles,
                ConflictReason: conflictReason));
        }

        if (cache is not null && cacheKey is not null)
        {
            await cache.SetAsync(cacheKey, result, DefenseCouncilCacheKeys.MemberCandidatesTtl, cancellationToken);
        }

        return Result.Success<IReadOnlyList<CouncilMemberCandidateDto>>(result);
    }

    private static string ComputeKeywordKey(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return "all";
        }

        var normalized = keyword.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
