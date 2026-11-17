using UGTM.Modules.ThesisManagement.Application.StudentGroups.GetAdvisingGroups;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

public sealed class AdvisingGroupQueryReader(ThesisManagementDbContext db, TimeProvider timeProvider) : IAdvisingGroupQueryReader
{
    public async Task<AdvisingGroupPage> GetAsync(Guid advisorId, GetAdvisingGroupsQuery filter, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        const string accents = "àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ";
        var ascii = GetAdvisingGroupsHandler.NormalizeSearch(accents);
        // Read-only projection across module schemas; no extra EF entities or migrations.
        // All request values are bound parameters, including the advisor scope.
        var scoped = db.Database.SqlQuery<AdvisingGroupRecord>($"""
            SELECT g.id AS group_id, g.topic_id AS topic_id, t.batch_id AS batch_id,
                b.batch_name AS batch_name, t.title_vi AS title_vi, t.title_en AS title_en,
                g.status AS status, g.created_at AS created_at,
                m.confirmed AS confirmed_students_count, m.pending AS pending_confirmations_count,
                m.expired AS expired_invitations_count,
                translate(lower(concat_ws(' ', t.title_vi, t.title_en, g.id::text, m.search)), {accents}, {ascii}) AS search_text
            FROM thesis.student_group g
            JOIN topics.topic t ON t.id = g.topic_id
            LEFT JOIN academic.kltn_batch b ON b.id = t.batch_id
            LEFT JOIN LATERAL (
                SELECT count(*) FILTER (WHERE sm.confirmation_status = 'Confirmed')::int AS confirmed,
                    count(*) FILTER (WHERE sm.confirmation_status = 'Pending' AND (sm.confirmation_expires_at_utc IS NULL OR sm.confirmation_expires_at_utc > {now}))::int AS pending,
                    count(*) FILTER (WHERE sm.confirmation_status = 'Pending' AND sm.confirmation_expires_at_utc <= {now})::int AS expired,
                    string_agg(concat_ws(' ', u.full_name, u.user_code, u.email), ' ') AS search
                FROM thesis.student_group_member sm
                LEFT JOIN identity."user" u ON u.id = sm.student_id
                WHERE sm.group_id = g.id
            ) m ON true
            WHERE t.advisor_id = {advisorId}
            """);
        if (filter.BatchId.HasValue) scoped = scoped.Where(group => group.BatchId == filter.BatchId.Value);
        var statistics = await scoped.GroupBy(_ => 1).Select(groups => new AdvisingStatisticsDto(
            groups.Count(), groups.Count(group => group.Status != "Completed" && group.Status != "Stopped"),
            groups.Count(group => group.Status == "Completed"), groups.Count(group => group.Status == "Stopped"),
            groups.Sum(group => group.Status != "Completed" && group.Status != "Stopped" ? group.ConfirmedStudentsCount : 0),
            groups.Sum(group => group.Status != "Completed" && group.Status != "Stopped" ? group.PendingConfirmationsCount : 0),
            groups.Sum(group => group.Status != "Completed" && group.Status != "Stopped" ? group.ExpiredInvitationsCount : 0)))
            .SingleOrDefaultAsync(cancellationToken) ?? new(0, 0, 0, 0, 0, 0, 0);
        var query = filter.Scope switch
        {
            "active" => scoped.Where(group => group.Status != "Completed" && group.Status != "Stopped"),
            "completed" => scoped.Where(group => group.Status == "Completed"),
            "stopped" => scoped.Where(group => group.Status == "Stopped"),
            _ => scoped
        };
        if (!string.IsNullOrEmpty(filter.Status)) query = query.Where(group => group.Status == filter.Status);
        if (filter.Confirmation == "pending") query = query.Where(group => group.PendingConfirmationsCount > 0);
        if (filter.Confirmation == "expired") query = query.Where(group => group.ExpiredInvitationsCount > 0);
        foreach (var word in GetAdvisingGroupsHandler.NormalizeSearch(filter.Keyword ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var pattern = "%" + word.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(group => EF.Functions.Like(group.SearchText, pattern, "\\"));
        }
        var total = await query.CountAsync(cancellationToken);
        var ascending = filter.SortOrder == "asc";
        var ordered = (filter.SortBy, ascending) switch
        {
            ("title", true) => query.OrderBy(group => group.TitleVi),
            ("title", false) => query.OrderByDescending(group => group.TitleVi),
            ("studentCount", true) => query.OrderBy(group => group.ConfirmedStudentsCount),
            ("studentCount", false) => query.OrderByDescending(group => group.ConfirmedStudentsCount),
            (_, true) => query.OrderBy(group => group.CreatedAt),
            _ => query.OrderByDescending(group => group.CreatedAt)
        };
        var offset = ((long)filter.PageNumber - 1) * filter.PageSize;
        var page = offset >= total ? [] : await ordered.ThenBy(group => group.GroupId)
            .Skip((int)offset).Take(filter.PageSize).ToListAsync(cancellationToken);
        var ids = page.Select(group => group.GroupId).ToArray();
        var members = ids.Length == 0 ? [] : await db.StudentGroupMembers.AsNoTracking()
            .Where(member => ids.Contains(member.GroupId))
            .Select(member => new AdvisingMemberRecord(member.GroupId, member.StudentId,
                member.ConfirmationStatus == Domain.Enums.MemberConfirmationStatus.Pending && member.ConfirmationExpiresAtUtc <= now
                    ? "Expired" : member.ConfirmationStatus.ToString(),
                member.JoinedAt, member.ConfirmedAtUtc, member.ConfirmationExpiresAtUtc)).ToListAsync(cancellationToken);
        return new(page, total, statistics, members);
    }
}
