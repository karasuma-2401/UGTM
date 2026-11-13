using System.Globalization;
using System.Data;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapIdentityAuditLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/audit-logs").WithTags("Identity.AuditLogs").RequireAuthorization("AdminOnly");
        group.MapGet("/", GetAuditLogsAsync).WithSummary("Tra cứu nhật ký kiểm toán");
        group.MapGet("/statistics", GetStatisticsAsync).WithSummary("Thống kê kết quả theo bộ lọc");
        group.MapGet("/actions", async (IAuditLogQueryReader reader, CancellationToken ct) =>
            Results.Ok(ApiResponse<List<string>>.Ok(await reader.GetActionsAsync(ct))))
            .WithSummary("Các mã thao tác đã ghi nhận");
        group.MapGet("/outbox-health", async (IEnumerable<IAuditIntegrationEventSource> sources, CancellationToken ct) =>
        {
            var health = new List<AuditOutboxHealth>();
            foreach (var source in sources) health.Add(await source.GetHealthAsync(ct));
            return Results.Ok(ApiResponse<List<AuditOutboxHealth>>.Ok(health));
        }).WithSummary("Độ trễ và lỗi chuyển sự kiện kiểm toán");
        group.MapGet("/export", ExportAsync).WithSummary("Xuất CSV nhật ký theo bộ lọc (tối đa 100.000 sự kiện)");
        group.MapGet("/{id:long}", GetByIdAsync).WithSummary("Chi tiết sự kiện kiểm toán");
        return endpoints;
    }

    private static async Task<IResult> GetAuditLogsAsync([AsParameters] AuditLogParameters p, GetAuditLogsHandler handler, CancellationToken cancellationToken)
        => (await handler.HandleAsync(p.ToQuery(), cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetStatisticsAsync([AsParameters] AuditLogParameters p, IAuditLogQueryReader reader, CancellationToken cancellationToken)
    {
        var query = GetAuditLogsHandler.NormalizeQuery(p.ToQuery() with { Outcome = null });
        if (query.IsFailure) return query.ToHttpResult();
        return Results.Ok(ApiResponse<AuditLogStatistics>.Ok(await reader.GetStatisticsAsync(query.Value, cancellationToken)));
    }

    private static async Task<IResult> GetByIdAsync(long id, IAuditLogQueryReader reader, CancellationToken cancellationToken)
    {
        var log = await reader.GetByIdAsync(id, cancellationToken);
        return log is null ? Results.NotFound() : Results.Ok(ApiResponse<UGTM.Modules.Identity.Application.AuditLogs.Dtos.AuditLogDto>.Ok(log));
    }

    private static async Task<IResult> ExportAsync([AsParameters] AuditLogParameters p, IAuditLogQueryReader reader, IdentityDbContext db, CancellationToken cancellationToken)
    {
        var query = GetAuditLogsHandler.NormalizeQuery(p.ToQuery());
        if (query.IsFailure) return query.ToHttpResult();
        // Count and export share one snapshot, including when new events arrive while streaming.
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            var count = await reader.GetAuditLogsAsync(query.Value with { PageNumber = 1, PageSize = 1 }, cancellationToken);
            if (count.TotalCount > 100_000)
            {
                await transaction.DisposeAsync();
                return Results.BadRequest(new { message = "Vui lòng thu hẹp bộ lọc: mỗi lần xuất tối đa 100.000 sự kiện." });
            }
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }

        return Results.Stream(async stream =>
        {
            await using var exportTransaction = transaction;
            await using var writer = new StreamWriter(stream, new UTF8Encoding(true), 4096, leaveOpen: true);
            await writer.WriteLineAsync("Id,TimestampUtc,ActorUserId,ActorEmail,ActorFullName,ActorSessionId,TargetType,TargetId,Action,Outcome,ReasonCode,RequestId,Metadata");
            await foreach (var log in reader.ExportAsync(query.Value, cancellationToken))
            {
                string?[] cells = [log.Id.ToString(CultureInfo.InvariantCulture), log.Timestamp.ToString("O", CultureInfo.InvariantCulture),
                    log.ActorUserId?.ToString(), log.ActorEmail, log.ActorFullName, log.ActorSessionId?.ToString(), log.TargetType,
                    log.TargetId, log.Action, log.Outcome, log.ReasonCode, log.RequestId, log.Metadata?.GetRawText()];
                await writer.WriteLineAsync(string.Join(',', cells.Select(CsvCell)));
            }
            await writer.FlushAsync(cancellationToken);
            await exportTransaction.CommitAsync(cancellationToken);
        }, "text/csv; charset=utf-8", "audit-logs.csv");
    }

    private static string CsvCell(string? value)
    {
        value ??= "";
        // Spreadsheet applications may execute untrusted fields as formulas.
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') ||
            value.TrimStart().StartsWith('@') || value.TrimStart().StartsWith('＝') || value.TrimStart().StartsWith('＋') ||
            value.TrimStart().StartsWith('－') || value.TrimStart().StartsWith('＠') || value.StartsWith('\t') || value.StartsWith('\r') || value.StartsWith('\n')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public sealed class AuditLogParameters
    {
        public Guid? ActorUserId { get; init; }
        public Guid? ActorSessionId { get; init; }
        public string? Action { get; init; }
        public string? TargetType { get; init; }
        public string? TargetId { get; init; }
        public string? RequestId { get; init; }
        public string? Outcome { get; init; }
        public string? SearchTerm { get; init; }
        public DateTime? FromUtc { get; init; }
        public DateTime? ToUtc { get; init; }
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SortBy { get; init; }
        public string? SortOrder { get; init; }
        public GetAuditLogsQuery ToQuery() => new(ActorUserId, Action, TargetType, Outcome, FromUtc, ToUtc, PageNumber ?? 1, PageSize ?? 20,
            SearchTerm, RequestId, TargetId, ActorSessionId, SortBy, SortOrder);
    }
}
