using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UGTM.Modules.ThesisManagement.Application.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Storage;

internal sealed class FinalSubmissionUploadCleanup(IServiceScopeFactory scopes, IThesisSubmissionObjectStorage storage,
    TimeProvider clock, ILogger<FinalSubmissionUploadCleanup> logger) : IFinalSubmissionUploadCleanup
{
    public async Task CleanAsync(Guid? attemptId, CancellationToken token = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var attempts = attemptId.HasValue
            ? await db.FinalSubmissionUploadAttempts.FromSqlInterpolated($"SELECT a.* FROM thesis.final_submission_upload_attempt a WHERE a.id={attemptId.Value} FOR UPDATE").ToListAsync(token)
            : await db.FinalSubmissionUploadAttempts.FromSqlInterpolated($"""
                SELECT a.* FROM thesis.final_submission_upload_attempt a
                WHERE (a.state IN ('Uploading','Cleaning') AND a.lease_expires_at_utc <= {now})
                   OR (a.state='CleanupPending' AND (a.retry_after_utc IS NULL OR a.retry_after_utc <= {now}))
                ORDER BY a.created_at_utc,a.id LIMIT 1 FOR UPDATE SKIP LOCKED
                """).ToListAsync(token);
        var attempt = attempts.SingleOrDefault();
        if (attempt is null || attempt.State == "Committed" || (attempt.State == "Cleaned" && !attemptId.HasValue)) return;
        if (attempt.State == "Cleaning" && attempt.LeaseExpiresAtUtc > now)
        {
            if (!attemptId.HasValue) return;
            // The request has finished its uploads, but an expired-lease cleanup is still doing
            // network IO. Queue another sweep so a late upload cannot escape the earlier delete.
            // The worker's final CAS requires Cleaning and cannot overwrite this pending sweep.
            attempt.State = "CleanupPending";
            attempt.RetryAfterUtc = now.AddSeconds(30);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return;
        }
        // A fresh context and a row lock wait for an uncertain commit before deciding to delete.
        if (attempt.CommittedPackageId.HasValue || await db.SubmissionFiles.AnyAsync(
            f => f.ObjectKey == attempt.PdfObjectKey || f.ObjectKey == attempt.DocxObjectKey, token)) return;
        attempt.State = "Cleaning";
        attempt.LeaseExpiresAtUtc = now.AddMinutes(2);
        var lease = attempt.LeaseExpiresAtUtc;
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        string? errorCode = null;
        try
        {
            await storage.DeleteAsync(attempt.PdfObjectKey, token);
            await storage.DeleteAsync(attempt.DocxObjectKey, token);
        }
        catch (Exception e) when (e is not OperationCanceledException || !token.IsCancellationRequested)
        {
            errorCode = "ObjectDeletionFailed";
            logger.LogWarning("Archival upload cleanup will retry attempt {AttemptId}.", attempt.Id);
        }
        // An interrupted deletion leaves a leased Cleaning row; the next worker can retry it.
        var next = clock.GetUtcNow().UtcDateTime.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(8, attempt.RetryCount + 1))));
        await db.FinalSubmissionUploadAttempts.Where(a => a.Id == attempt.Id && a.State == "Cleaning" && a.LeaseExpiresAtUtc == lease)
            .ExecuteUpdateAsync(update => update.SetProperty(a => a.State, errorCode == null ? "Cleaned" : "CleanupPending")
                .SetProperty(a => a.RetryCount, a => a.RetryCount + 1)
                .SetProperty(a => a.RetryAfterUtc, errorCode == null ? (DateTime?)null : next)
                .SetProperty(a => a.LastErrorCode, errorCode), token);
    }
}

internal sealed class FinalSubmissionUploadCleanupWorker(IServiceScopeFactory scopes,
    ILogger<FinalSubmissionUploadCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IFinalSubmissionUploadCleanup>().CleanAsync(null, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogWarning(e, "Archival cleanup worker could not finish its iteration."); }
        }
    }
}
