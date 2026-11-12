using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Contracts.Auditing;

namespace UGTM.Modules.Identity.Infrastructure.Auditing;

public sealed class AuditIntegrationEventProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<AuditIntegrationEventProcessor> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private DateTime _lastHealthCheckUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessSourcesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Audit integration event batch failed.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down gracefully.
        }
    }

    private async Task ProcessSourcesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sources = scope.ServiceProvider
            .GetServices<IAuditIntegrationEventSource>()
            .ToList();
        var consumer = scope.ServiceProvider.GetRequiredService<AuditIntegrationEventConsumer>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        foreach (var source in sources)
        {
            try
            {
                await source.ProcessBatchAsync(
                    consumer.ConsumeAsync,
                    clock.UtcNow,
                    BatchSize,
                    cancellationToken);

                if (clock.UtcNow - _lastHealthCheckUtc >= TimeSpan.FromMinutes(1))
                {
                    var health = await source.GetHealthAsync(cancellationToken);
                    if (health.FailedCount > 0 || (health.OldestPendingUtc is { } oldest && clock.UtcNow - oldest > TimeSpan.FromMinutes(5)))
                        logger.LogWarning("Audit outbox {SourceName}: {PendingCount} pending, {FailedCount} retrying, oldest {OldestPendingUtc}.",
                            health.Source, health.PendingCount, health.FailedCount, health.OldestPendingUtc);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Audit integration source {SourceName} failed.",
                    source.SourceName);
            }
        }
        if (clock.UtcNow - _lastHealthCheckUtc >= TimeSpan.FromMinutes(1)) _lastHealthCheckUtc = clock.UtcNow;
    }

}
