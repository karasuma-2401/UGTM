using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Api.ReviewerAssignments;

/// <summary>
/// Only DefenseCouncil writes. The other four contexts enlist to retain their ownership-specific
/// read locks on the same transaction. The SCRUM-103 two-context coordinator remains unchanged.
/// </summary>
public sealed class ReviewerAssignmentTransactionCoordinator(
    DefenseCouncilDbContext defense, ThesisManagementDbContext thesis, TopicCatalogDbContext topics,
    AcademicPeriodsDbContext academic, IdentityDbContext identity) : IReviewerAssignmentTransactionCoordinator
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<ReviewerAssignmentTransactionResult<T>>> workflow,
        CancellationToken cancellationToken = default) => RunAsync(workflow, null, cancellationToken);

    public Task<T> ExecuteWithPostSaveAsync<T>(Func<CancellationToken, Task<ReviewerAssignmentTransactionResult<T>>> workflow,
        Func<T, CancellationToken, Task<T>> postSave, CancellationToken cancellationToken = default)
        => RunAsync(workflow, postSave ?? throw new ArgumentNullException(nameof(postSave)), cancellationToken);

    private async Task<T> RunAsync<T>(Func<CancellationToken, Task<ReviewerAssignmentTransactionResult<T>>> workflow,
        Func<T, CancellationToken, Task<T>>? postSave, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        DbContext[] readers = [thesis, topics, academic, identity];
        DbContext[] all = [defense, .. readers];
        if (all.Any(context => context.Database.CurrentTransaction is not null || context.ChangeTracker.HasChanges()))
            throw new InvalidOperationException("Reviewer coordination requires clean contexts and owns its transaction.");
        var originals = readers.Select(context => context.Database.GetDbConnection()).ToArray();
        if (originals.Any(connection => connection.State != ConnectionState.Closed))
            throw new InvalidOperationException("Reader connections must be closed before reviewer coordination.");
        var connection = defense.Database.GetDbConnection();
        var opened = connection.State == ConnectionState.Closed;
        var enlisted = new List<IDbContextTransaction>();
        var replaced = 0;
        try
        {
            if (opened) await defense.Database.OpenConnectionAsync(token);
            await using var transaction = await defense.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            try
            {
                foreach (var reader in readers)
                {
                    reader.Database.SetDbConnection(connection, contextOwnsConnection: false);
                    replaced++;
                    enlisted.Add((await reader.Database.UseTransactionAsync(transaction.GetDbTransaction(), token))!);
                }
                var outcome = await workflow(token);
                if (!outcome.ShouldCommit)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    foreach (var context in all) context.ChangeTracker.Clear();
                    return outcome.Value;
                }
                EnsureReadersUnchanged(readers);
                await defense.SaveChangesAsync(acceptAllChangesOnSuccess: false, token);
                var value = outcome.Value;
                if (postSave is not null)
                {
                    defense.ChangeTracker.AcceptAllChanges();
                    value = await postSave(value, token);
                    EnsureReadersUnchanged(readers);
                    await defense.SaveChangesAsync(acceptAllChangesOnSuccess: false, token);
                }
                await transaction.CommitAsync(token);
                defense.ChangeTracker.AcceptAllChanges();
                return value;
            }
            catch (Exception error)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                foreach (var context in all) context.ChangeTracker.Clear();
                if (error is not DefenseCouncilConcurrencyException && IsPostgresConcurrency(error))
                    throw new DefenseCouncilConcurrencyException(error);
                throw;
            }
            finally
            {
                foreach (var enlistment in Enumerable.Reverse(enlisted)) await enlistment.DisposeAsync();
            }
        }
        finally
        {
            for (var index = 0; index < replaced; index++)
                readers[index].Database.SetDbConnection(originals[index], contextOwnsConnection: true);
            if (opened) await defense.Database.CloseConnectionAsync();
        }
    }

    private static void EnsureReadersUnchanged(IEnumerable<DbContext> readers)
    {
        if (readers.Any(context => context.ChangeTracker.HasChanges()))
            throw new InvalidOperationException("Reviewer assignment may not write another module's schema.");
    }

    private static bool IsPostgresConcurrency(Exception error)
        => error is PostgresException
        {
            SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.LockNotAvailable
        } || (error.InnerException is { } inner && IsPostgresConcurrency(inner));
}
