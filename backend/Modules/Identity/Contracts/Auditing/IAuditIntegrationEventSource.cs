namespace UGTM.Modules.Identity.Contracts.Auditing;

public interface IAuditIntegrationEventSource
{
    string SourceName { get; }

    Task<AuditOutboxHealth> GetHealthAsync(CancellationToken cancellationToken);

    Task ProcessBatchAsync(
        Func<AuditIntegrationEvent, CancellationToken, Task> handler,
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken);
}

public sealed record AuditOutboxHealth(string Source, int PendingCount, int FailedCount, DateTime? OldestPendingUtc);
