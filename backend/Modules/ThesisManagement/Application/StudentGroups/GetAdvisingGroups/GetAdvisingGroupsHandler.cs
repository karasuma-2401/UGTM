using System.Globalization;
using System.Text;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.StudentGroups.GetAdvisingGroups;

public sealed record GetAdvisingGroupsQuery(Guid? BatchId = null, string? Keyword = null,
    string Scope = "active", string? Status = null, string? Confirmation = null,
    string SortBy = "createdAt", string SortOrder = "desc", int PageNumber = 1, int PageSize = 10);

public sealed record AdvisingStatisticsDto(int Total, int Active, int Completed, int Stopped,
    int ActiveConfirmedStudents, int PendingInvitations, int ExpiredInvitations);
public sealed record AdvisingMemberRecord(Guid GroupId, Guid StudentId, string ConfirmationStatus,
    DateTime JoinedAt, DateTime? ConfirmedAtUtc, DateTime? ConfirmationExpiresAtUtc);
public sealed record AdvisingStudentDto(Guid StudentId, string? UserCode, string? FullName,
    string ConfirmationStatus, DateTime JoinedAt, DateTime? ConfirmedAtUtc, DateTime? ConfirmationExpiresAtUtc);
public sealed record AdvisingGroupDto(Guid GroupId, Guid TopicId, string TitleVi, string? TitleEn,
    Guid BatchId, string? BatchName, string Status, DateTime CreatedAt, int ConfirmedStudentsCount,
    int PendingConfirmationsCount, int ExpiredInvitationsCount, IReadOnlyList<AdvisingStudentDto> Students);
public sealed record AdvisingGroupsDto(PagedResult<AdvisingGroupDto> Page, AdvisingStatisticsDto Statistics);

// This read model deliberately includes historical groups. Registration occupancy
// continues to use the separate contract that excludes stopped groups.
public sealed class AdvisingGroupRecord
{
    public Guid GroupId { get; set; }
    public Guid TopicId { get; set; }
    public Guid BatchId { get; set; }
    public string? BatchName { get; set; }
    public string TitleVi { get; set; } = "";
    public string? TitleEn { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int ConfirmedStudentsCount { get; set; }
    public int PendingConfirmationsCount { get; set; }
    public int ExpiredInvitationsCount { get; set; }
    public string SearchText { get; set; } = "";
}
public sealed record AdvisingGroupPage(IReadOnlyList<AdvisingGroupRecord> Items, int TotalCount,
    AdvisingStatisticsDto Statistics, IReadOnlyList<AdvisingMemberRecord> Members);
public interface IAdvisingGroupQueryReader
{
    Task<AdvisingGroupPage> GetAsync(Guid advisorId, GetAdvisingGroupsQuery query, CancellationToken cancellationToken);
}

public sealed class GetAdvisingGroupsHandler(IAdvisingGroupQueryReader reader,
    IStudentInfoReader students, ICurrentUser currentUser)
{
    public async Task<Result<AdvisingGroupsDto>> HandleAsync(GetAdvisingGroupsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } advisorId || advisorId == Guid.Empty)
            return Result.Failure<AdvisingGroupsDto>(Error.Unauthorized("Advising.AuthenticationRequired", "Vui lòng đăng nhập để xem nhóm hướng dẫn."));
        if (!currentUser.Roles.Any(role => role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
            return Result.Failure<AdvisingGroupsDto>(Error.Forbidden("Advising.AccessDenied", "Bạn không có quyền xem nhóm hướng dẫn."));

        if (query.BatchId == Guid.Empty || query.Keyword?.Length > 200
            || !new[] { "all", "active", "completed", "stopped" }.Contains(query.Scope)
            || !new[] { "createdAt", "title", "studentCount" }.Contains(query.SortBy)
            || !new[] { "asc", "desc" }.Contains(query.SortOrder)
            || (!string.IsNullOrEmpty(query.Confirmation) && !new[] { "pending", "expired" }.Contains(query.Confirmation))
            || (!string.IsNullOrEmpty(query.Status) && (!Enum.TryParse<StudentGroupStatus>(query.Status, out var status)
                || !Enum.IsDefined(status) || int.TryParse(query.Status, out _))))
            return Result.Failure<AdvisingGroupsDto>(Error.Validation("Advising.InvalidFilter", "Bộ lọc nhóm hướng dẫn không hợp lệ."));

        var sanitized = query with { Keyword = query.Keyword?.Trim(), PageNumber = Math.Max(1, query.PageNumber), PageSize = Math.Clamp(query.PageSize, 1, 100) };
        // Always use the signed-in advisor, including accounts that also have Admin.
        var result = await reader.GetAsync(advisorId, sanitized, cancellationToken);
        var identities = await students.GetStudentsByIdsAsync(result.Members.Select(member => member.StudentId).Distinct(), cancellationToken);
        var members = result.Members.GroupBy(member => member.GroupId).ToDictionary(group => group.Key, group => group
            .OrderBy(member => member.JoinedAt).ThenBy(member => member.StudentId).Select(member =>
            {
                identities.TryGetValue(member.StudentId, out var identity);
                return new AdvisingStudentDto(member.StudentId, identity?.UserCode, identity?.FullName,
                    member.ConfirmationStatus, member.JoinedAt, member.ConfirmedAtUtc, member.ConfirmationExpiresAtUtc);
            }).ToArray());
        var items = result.Items.Select(group => new AdvisingGroupDto(group.GroupId, group.TopicId,
            group.TitleVi, group.TitleEn, group.BatchId, group.BatchName, group.Status, group.CreatedAt,
            group.ConfirmedStudentsCount, group.PendingConfirmationsCount, group.ExpiredInvitationsCount,
            members.GetValueOrDefault(group.GroupId) ?? [])).ToArray();
        return Result.Success(new AdvisingGroupsDto(new(items, result.TotalCount, sanitized.PageNumber, sanitized.PageSize), result.Statistics));
    }

    public static string NormalizeSearch(string value) => new string(value.Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray())
        .Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant().Normalize(NormalizationForm.FormC);
}
