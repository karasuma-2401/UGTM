namespace UGTM.Modules.Identity.Contracts.Auditing;

public interface IAuditIntegrationEventSource
{
    string SourceName { get; }

    Task ProcessBatchAsync(
        Func<AuditIntegrationEvent, CancellationToken, Task> handler,
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken);
}
