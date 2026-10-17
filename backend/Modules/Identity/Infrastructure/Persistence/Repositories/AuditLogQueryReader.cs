using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.AuditLogs.Dtos;
using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class AuditLogQueryReader(IdentityDbContext db) : IAuditLogQueryReader
{
    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        var source = db.AuditLogs.AsNoTracking();

        if (query.ActorUserId.HasValue)
        {
            source = source.Where(x => x.ActorUserId == query.ActorUserId.Value);
        }

        if (query.Action is not null)
        {
            source = source.Where(x => x.Action == query.Action);
        }

        if (query.TargetType is not null)
        {
            source = source.Where(x => x.TargetType == query.TargetType);
        }

        if (query.Outcome is not null)
        {
            source = source.Where(x => x.Outcome == query.Outcome);
        }

        if (query.FromUtc.HasValue)
        {
            source = source.Where(x => x.Timestamp >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            source = source.Where(x => x.Timestamp < query.ToUtc.Value);
        }

        var totalCount = await source.CountAsync(cancellationToken);

        var rows = await (
                from auditLog in source
                join user in db.Users.AsNoTracking()
                    on auditLog.ActorUserId equals (Guid?)user.Id into actors
                from actor in actors.DefaultIfEmpty()
                orderby auditLog.Timestamp descending, auditLog.Id descending
                select new
                {
                    auditLog.Id,
                    auditLog.ActorUserId,
                    ActorEmail = actor == null ? null : actor.Email,
                    ActorFullName = actor == null ? null : actor.FullName,
                    auditLog.ActorSessionId,
                    auditLog.TargetType,
                    auditLog.TargetId,
                    auditLog.Action,
                    auditLog.Outcome,
                    auditLog.ReasonCode,
                    auditLog.RequestId,
                    auditLog.MetadataJson,
                    auditLog.Timestamp
                })
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new AuditLogDto(
            x.Id,
            x.ActorUserId,
            x.ActorEmail,
            x.ActorFullName,
            x.ActorSessionId,
            x.TargetType,
            x.TargetId,
            x.Action,
            x.Outcome,
            x.ReasonCode,
            x.RequestId,
            ParseMetadata(x.MetadataJson),
            x.Timestamp)).ToList();

        return new PagedResult<AuditLogDto>(
            items,
            totalCount,
            query.PageNumber,
            query.PageSize);
    }

    private static JsonElement? ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // A malformed legacy row must not make the whole result page unusable.
            return null;
        }
    }
}
