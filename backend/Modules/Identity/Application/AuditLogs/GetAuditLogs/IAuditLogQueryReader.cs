using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.AuditLogs.Dtos;

namespace UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;

public interface IAuditLogQueryReader
{
    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        GetAuditLogsQuery query,
        CancellationToken cancellationToken);

    Task<AuditLogStatistics> GetStatisticsAsync(GetAuditLogsQuery query, CancellationToken cancellationToken);
    Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<List<string>> GetActionsAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<AuditLogDto> ExportAsync(GetAuditLogsQuery query, CancellationToken cancellationToken);
}

public sealed record AuditLogStatistics(int Total, int Success, int Denied, int Failed, int Other);
