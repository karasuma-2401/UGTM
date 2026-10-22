using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Modules.ThesisManagement.Application.ReviewGateDeadlines;

namespace UGTM.Modules.ThesisManagement.Infrastructure.ReviewGateDeadlines;

public sealed class ReviewGateDeadlineEnforcementWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReviewGateDeadlineEnforcementOptions> options,
    ILogger<ReviewGateDeadlineEnforcementWorker> logger) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await EnforceAsync(stoppingToken);
            using var timer = new PeriodicTimer(_pollingInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await EnforceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down gracefully.
        }
    }

    private async Task EnforceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var enforcer = scope.ServiceProvider.GetRequiredService<IReviewGateDeadlineEnforcer>();
            var result = await enforcer.EnforceAsync(cancellationToken);
            if (result.ClosedGroupCount > 0)
            {
                logger.LogInformation(
                    "Closed {ClosedGroupCount} thesis groups whose review gates reached final closure.",
                    result.ClosedGroupCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Review gate deadline enforcement failed.");
        }
    }
}
