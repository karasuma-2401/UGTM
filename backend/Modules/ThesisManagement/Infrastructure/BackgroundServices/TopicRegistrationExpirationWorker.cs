using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Expiration;

namespace UGTM.Modules.ThesisManagement.Infrastructure.BackgroundServices;

public sealed class TopicRegistrationExpirationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TopicRegistrationExpirationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await EnforceAsync(stoppingToken);
            using var timer = new PeriodicTimer(PollingInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await EnforceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
    }

    private async Task EnforceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var enforcer = scope.ServiceProvider.GetRequiredService<ITopicRegistrationExpirationEnforcer>();
            var result = await enforcer.EnforceAsync(cancellationToken);
            if (result.ExpiredCount > 0)
            {
                logger.LogInformation(
                    "Topic registration cleanup: processed {ExpiredCount} expired pending registrations ({BlockedCount} students blocked).",
                    result.ExpiredCount,
                    result.BlockedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enforce topic registration expiration.");
        }
    }
}
