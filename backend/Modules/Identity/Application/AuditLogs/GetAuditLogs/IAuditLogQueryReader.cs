using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.AuditLogs.Dtos;

namespace UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;

public interface IAuditLogQueryReader
{
    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        GetAuditLogsQuery query,
        CancellationToken cancellationToken);
}
