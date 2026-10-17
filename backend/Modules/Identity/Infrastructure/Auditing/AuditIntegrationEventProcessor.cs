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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
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
    }

}
