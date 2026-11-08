using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Storage;

internal sealed class ThesisUploadLifecycle(ThesisManagementDbContext db, IServiceScopeFactory scopes,
    IThesisSubmissionObjectStorage storage, TimeProvider clock, ILogger<ThesisUploadLifecycle> logger) : IThesisUploadLifecycle, IThesisUploadCleanup
{
    public async Task<Guid> BeginAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var attempt = new ThesisUploadAttempt
        {
            Id = Guid.NewGuid(),
            ObjectKey = objectKey,
            CreatedAtUtc = now,
            LeaseExpiresAtUtc = now.AddMinutes(10)
        };
        // The attempt must survive even if the request's eventual save fails.
        await using var scope = scopes.CreateAsyncScope();
        var independent = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        independent.ThesisUploadAttempts.Add(attempt);
        await independent.SaveChangesAsync(cancellationToken);
        return attempt.Id;
    }

    public async Task<Error?> SaveAsync(Guid attemptId, Func<CancellationToken, Task<Error?>> save, Error expiredError, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var attempt = await db.ThesisUploadAttempts.FromSqlInterpolated($"""
            SELECT a.* FROM thesis.submission_upload_attempt a WHERE a.id = {attemptId} FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
        if (attempt is null || attempt.State != "Uploading" || attempt.LeaseExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
            return expiredError;
        attempt.State = "Committed";
        var error = await save(cancellationToken);
        if (error is not null) return error;
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    public async Task FinishAsync(Guid attemptId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { await CleanAsync(attemptId, timeout.Token); }
        catch (Exception error) { logger.LogWarning("Submission upload attempt {AttemptId} cleanup failed with {ErrorCode}; it will be retried.", attemptId, error.GetType().Name); }
    }

    public Task CleanExpiredAsync(CancellationToken cancellationToken = default) => CleanAsync(null, cancellationToken);

    private async Task CleanAsync(Guid? attemptId, CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var cleanupDb = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        await using var transaction = await cleanupDb.Database.BeginTransactionAsync(token);
        var attempt = attemptId.HasValue
            ? await cleanupDb.ThesisUploadAttempts.FromSqlInterpolated($"SELECT a.* FROM thesis.submission_upload_attempt a WHERE a.id = {attemptId.Value} FOR UPDATE").SingleOrDefaultAsync(token)
            : await cleanupDb.ThesisUploadAttempts.FromSqlInterpolated($"""
                SELECT a.* FROM thesis.submission_upload_attempt a
                WHERE (a.state IN ('Uploading','Cleaning') AND a.lease_expires_at_utc <= {now})
                   OR (a.state = 'CleanupPending' AND (a.retry_after_utc IS NULL OR a.retry_after_utc <= {now}))
                   OR (a.state = 'Cleaned' AND a.retry_after_utc <= {now})
                ORDER BY a.created_at_utc, a.id LIMIT 1 FOR UPDATE SKIP LOCKED
                """).SingleOrDefaultAsync(token);
        if (attempt is null || attempt.State == "Committed") return;
        if (attempt.State == "Cleaning" && attempt.LeaseExpiresAtUtc > now)
        {
            if (!attemptId.HasValue) return;
            attempt.State = "CleanupPending";
            attempt.RetryAfterUtc = now.AddSeconds(30);
            await cleanupDb.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return;
        }
        // Waiting for this row lock also waits for an ambiguous database commit.
        if (await cleanupDb.SubmissionFiles.AnyAsync(file => file.ObjectKey == attempt.ObjectKey, token))
        {
            attempt.State = "Committed";
            await cleanupDb.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return;
        }
        attempt.State = "Cleaning";
        attempt.LeaseExpiresAtUtc = now.AddMinutes(2);
        var lease = attempt.LeaseExpiresAtUtc;
        await cleanupDb.SaveChangesAsync(token);
        await transaction.CommitAsync(token);

        string? errorCode = null;
        try { await storage.DeleteAsync(attempt.ObjectKey, token); }
        catch (Exception error) when (error is not OperationCanceledException || !token.IsCancellationRequested)
        {
            errorCode = "ObjectDeletionFailed";
            logger.LogWarning("Submission upload cleanup will retry attempt {AttemptId}.", attempt.Id);
        }
        var next = clock.GetUtcNow().UtcDateTime.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(8, attempt.RetryCount + 1))));
        // Repeat successful sweeps for a day: a storage server can finish accepting an
        // upload after the application process dies and an earlier delete has completed.
        var resweep = now < attempt.CreatedAtUtc.AddDays(1) ? now.AddMinutes(15) : (DateTime?)null;
        await cleanupDb.ThesisUploadAttempts.Where(a => a.Id == attempt.Id && a.State == "Cleaning" && a.LeaseExpiresAtUtc == lease)
            .ExecuteUpdateAsync(update => update.SetProperty(a => a.State, errorCode == null ? "Cleaned" : "CleanupPending")
                .SetProperty(a => a.RetryCount, a => a.RetryCount + 1)
                .SetProperty(a => a.RetryAfterUtc, errorCode == null ? resweep : next)
                .SetProperty(a => a.LastErrorCode, errorCode), token);
    }
}

internal sealed class ThesisUploadCleanupWorker(IServiceScopeFactory scopes, ILogger<ThesisUploadCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IThesisUploadCleanup>().CleanExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning(error, "Submission upload cleanup iteration failed."); }
        }
    }
}
