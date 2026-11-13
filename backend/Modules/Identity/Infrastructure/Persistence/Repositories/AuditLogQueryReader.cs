using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.AuditLogs.Dtos;
using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class AuditLogQueryReader(IdentityDbContext db) : IAuditLogQueryReader
{
    private IQueryable<AuditLog> Filter(GetAuditLogsQuery query, bool includeOutcome = true)
    {
        var source = db.AuditLogs.AsNoTracking();
        if (query.ActorUserId.HasValue) source = source.Where(x => x.ActorUserId == query.ActorUserId);
        if (query.ActorSessionId.HasValue) source = source.Where(x => x.ActorSessionId == query.ActorSessionId);
        if (query.Action is not null) source = source.Where(x => x.Action == query.Action);
        if (query.TargetType is not null) source = source.Where(x => x.TargetType == query.TargetType);
        if (query.TargetId is not null) source = source.Where(x => x.TargetId == query.TargetId);
        if (query.RequestId is not null) source = source.Where(x => x.RequestId == query.RequestId);
        if (includeOutcome && query.Outcome is not null)
            source = query.Outcome == "Success"
                ? source.Where(x => x.Outcome == "Success" || x.Outcome == "Succeeded")
                : source.Where(x => x.Outcome == query.Outcome);
        if (query.FromUtc.HasValue) source = source.Where(x => x.Timestamp >= query.FromUtc);
        if (query.ToUtc.HasValue) source = source.Where(x => x.Timestamp < query.ToUtc);
        if (query.SearchTerm is not null)
        {
            // Escape SQL LIKE wildcards so the search has literal substring semantics.
            var pattern = "%" + query.SearchTerm.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            // jsonb has no ILIKE operator; cast it to text in a parameterized subquery.
            var metadataIds = db.AuditLogs.FromSqlInterpolated($"SELECT * FROM identity.audit_log WHERE metadata_json::text ILIKE {pattern}")
                .Select(x => x.Id);
            source = source.Where(x => EF.Functions.ILike(x.TargetType, pattern)
                || EF.Functions.ILike(x.TargetId, pattern)
                || EF.Functions.ILike(x.Action, pattern)
                || (x.ReasonCode != null && EF.Functions.ILike(x.ReasonCode, pattern))
                || EF.Functions.ILike(x.RequestId, pattern)
                || metadataIds.Contains(x.Id)
                || db.Users.Any(u => u.Id == x.ActorUserId &&
                    (EF.Functions.ILike(u.FullName, pattern) || EF.Functions.ILike(u.Email, pattern))));
        }
        return source;
    }

    private IQueryable<AuditRow> Project(IQueryable<AuditLog> source) =>
        from log in source
        join user in db.Users.AsNoTracking() on log.ActorUserId equals (Guid?)user.Id into users
        from actor in users.DefaultIfEmpty()
        select new AuditRow(log.Id, log.ActorUserId, actor == null ? null : actor.Email,
            actor == null ? null : actor.FullName, log.ActorSessionId, log.TargetType, log.TargetId,
            log.Action, log.Outcome, log.ReasonCode, log.RequestId, log.MetadataJson, log.Timestamp);

    private IOrderedQueryable<AuditLog> ApplySorting(IQueryable<AuditLog> logs, GetAuditLogsQuery query)
    {
        var isAsc = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        return (query.SortBy?.ToLowerInvariant()) switch
        {
            "actor" => isAsc
                ? logs.OrderBy(x => db.Users.Where(u => u.Id == x.ActorUserId).Select(u => u.FullName ?? u.Email).FirstOrDefault() == null)
                    .ThenBy(x => db.Users.Where(u => u.Id == x.ActorUserId).Select(u => u.FullName ?? u.Email).FirstOrDefault())
                    .ThenByDescending(x => x.Id)
                : logs.OrderBy(x => db.Users.Where(u => u.Id == x.ActorUserId).Select(u => u.FullName ?? u.Email).FirstOrDefault() == null)
                    .ThenByDescending(x => db.Users.Where(u => u.Id == x.ActorUserId).Select(u => u.FullName ?? u.Email).FirstOrDefault())
                    .ThenByDescending(x => x.Id),
            "action" => isAsc
                ? logs.OrderBy(x => x.Action).ThenByDescending(x => x.Id)
                : logs.OrderByDescending(x => x.Action).ThenByDescending(x => x.Id),
            "outcome" => isAsc
                ? logs.OrderBy(x => x.Outcome).ThenByDescending(x => x.Id)
                : logs.OrderByDescending(x => x.Outcome).ThenByDescending(x => x.Id),
            _ => isAsc
                ? logs.OrderBy(x => x.Timestamp).ThenBy(x => x.Id)
                : logs.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id),
        };
    }

    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(GetAuditLogsQuery query, CancellationToken cancellationToken)
    {
        var source = Filter(query);
        var total = await source.CountAsync(cancellationToken);
        var offset = ((long)query.PageNumber - 1) * query.PageSize;
        if (offset > int.MaxValue) return new PagedResult<AuditLogDto>([], total, query.PageNumber, query.PageSize);
        var rows = await Project(ApplySorting(source, query)
            .Skip((int)offset).Take(query.PageSize)).ToListAsync(cancellationToken);
        return new PagedResult<AuditLogDto>(rows.Select(ToDto).ToList(), total, query.PageNumber, query.PageSize);
    }

    public async Task<AuditLogStatistics> GetStatisticsAsync(GetAuditLogsQuery query, CancellationToken cancellationToken)
    {
        var groups = await Filter(query, false).GroupBy(x => x.Outcome)
            .Select(g => new { Outcome = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        return new AuditLogStatistics(groups.Sum(x => x.Count), groups.Where(x => x.Outcome is "Success" or "Succeeded").Sum(x => x.Count),
            groups.Where(x => x.Outcome == "Denied").Sum(x => x.Count), groups.Where(x => x.Outcome == "Failed").Sum(x => x.Count),
            groups.Where(x => x.Outcome is not ("Success" or "Succeeded" or "Denied" or "Failed")).Sum(x => x.Count));
    }

    public async Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var row = await Project(db.AuditLogs.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : ToDto(row);
    }

    public Task<List<string>> GetActionsAsync(CancellationToken cancellationToken) =>
        db.AuditLogs.AsNoTracking().Select(x => x.Action).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);

    public async IAsyncEnumerable<AuditLogDto> ExportAsync(GetAuditLogsQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = Project(ApplySorting(Filter(query), query));
        await foreach (var row in rows.AsAsyncEnumerable().WithCancellation(cancellationToken)) yield return ToDto(row);
    }

    private static AuditLogDto ToDto(AuditRow row) => new(row.Id, row.ActorUserId, row.ActorEmail, row.ActorFullName,
        row.ActorSessionId, row.TargetType, row.TargetId, row.Action, row.Outcome == "Succeeded" ? "Success" : row.Outcome,
        row.ReasonCode, row.RequestId, ParseMetadata(row.MetadataJson), row.Timestamp);

    private static JsonElement? ParseMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { using var document = JsonDocument.Parse(json); return document.RootElement.Clone(); }
        catch (JsonException) { return null; }
    }

    private sealed record AuditRow(long Id, Guid? ActorUserId, string? ActorEmail, string? ActorFullName,
        Guid? ActorSessionId, string TargetType, string TargetId, string Action, string Outcome,
        string? ReasonCode, string RequestId, string? MetadataJson, DateTime Timestamp);
}
