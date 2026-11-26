using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UGTM.Modules.Notifications.Infrastructure.Reminders;

public sealed class DeadlineReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<DeadlineReminderOptions> configuredOptions,
    ILogger<DeadlineReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = configuredOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("DeadlineReminderBackgroundService đang tắt theo cấu hình Notifications:Reminders:Enabled=false.");
            return;
        }

        var intervalMinutes = Math.Clamp(options.ScanIntervalMinutes, 1, 120);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        try
        {
            await RunCycleAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var coordinator = scope.ServiceProvider.GetRequiredService<IDeadlineReminderCoordinator>();
            await coordinator.RunOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeadlineReminderBackgroundService gặp lỗi trong chu kỳ quét nhắc hạn.");
        }
    }
}
