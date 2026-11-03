using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.BackgroundServices;

public sealed class PlagiarismScanWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PlagiarismAiOptions> options,
    ILogger<PlagiarismScanWorker> logger) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(
        Math.Clamp(options.Value.PollingIntervalSeconds, 5, 300));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "PlagiarismScanWorker started. Polling interval: {IntervalSeconds}s.",
            _pollingInterval.TotalSeconds);

        try
        {
            await ProcessCycleAsync(stoppingToken);
            using var timer = new PeriodicTimer(_pollingInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("PlagiarismScanWorker is stopping gracefully.");
        }
    }

    private async Task ProcessCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var aiClient = scope.ServiceProvider.GetRequiredService<IPlagiarismAiClient>();
            var scanRepository = scope.ServiceProvider.GetRequiredService<IFinalReportPlagiarismScanRepository>();
            var executeHandler = scope.ServiceProvider.GetRequiredService<ExecutePlagiarismScanHandler>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IThesisUnitOfWork>();

            // 1. Health check to detect if Colab GPU / AI service is online
            var isAiHealthy = await aiClient.CheckHealthAsync(cancellationToken);

            // 2. If healthy, auto-rescue any ComputeUnavailable or WaitingForCompute scans
            if (isAiHealthy)
            {
                var batchSize = Math.Clamp(options.Value.BatchResumeSize, 1, 100);
                var resumableScans = await scanRepository.GetResumableScansAsync(batchSize, cancellationToken);
                if (resumableScans.Count > 0)
                {
                    logger.LogInformation(
                        "AI service is online. Auto-resuming {Count} stalled or compute-unavailable scans.",
                        resumableScans.Count);

                    var nowUtc = DateTime.UtcNow;
                    foreach (var scan in resumableScans)
                    {
                        scan.ResetForRetry(nowUtc);
                    }

                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }

            // 3. Process active or pending scans ready for execution or poll
            var activeScans = await scanRepository.GetActiveOrPendingScansAsync(limit: 20, cancellationToken);
            foreach (var scan in activeScans)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await executeHandler.ExecuteAsync(scan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Error processing plagiarism scan {ScanId} for FinalReport {ReportId} v{Version}",
                        scan.Id,
                        scan.FinalReportId,
                        scan.VersionNo);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run plagiarism scan worker cycle.");
        }
    }
}

