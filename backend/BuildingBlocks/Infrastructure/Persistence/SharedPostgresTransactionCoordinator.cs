using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace UGTM.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Executes a callback and persists two module contexts through one PostgreSQL transaction.
/// Contexts continue to enforce their own schema write ownership.
/// </summary>
public static class SharedPostgresTransactionCoordinator
{
    public static Task<TResult> ExecuteAsync<TResult>(
        DbContext primaryContext,
        DbContext secondaryContext,
        Func<CancellationToken, Task<TResult>> workflow,
        Func<TResult, CancellationToken, Task<TResult>>? postSave,
        CancellationToken cancellationToken = default)
        => ExecuteCoreAsync(primaryContext, secondaryContext, workflow, postSave, null, cancellationToken);

    public static Task<TResult> ExecuteConditionalAsync<TResult>(
        DbContext primaryContext,
        DbContext secondaryContext,
        Func<CancellationToken, Task<TResult>> workflow,
        Func<TResult, bool> shouldCommit,
        CancellationToken cancellationToken = default)
        => ExecuteCoreAsync(primaryContext, secondaryContext, workflow, null,
            shouldCommit ?? throw new ArgumentNullException(nameof(shouldCommit)), cancellationToken);

    private static async Task<TResult> ExecuteCoreAsync<TResult>(
        DbContext primaryContext,
        DbContext secondaryContext,
        Func<CancellationToken, Task<TResult>> workflow,
        Func<TResult, CancellationToken, Task<TResult>>? postSave,
        Func<TResult, bool>? shouldCommit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(primaryContext);
        ArgumentNullException.ThrowIfNull(secondaryContext);
        ArgumentNullException.ThrowIfNull(workflow);

        if (primaryContext.Database.CurrentTransaction is not null
            || secondaryContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Cross-module coordination cannot join an existing transaction.");
        }

        var primaryConnection = primaryContext.Database.GetDbConnection();
        var secondaryConnection = secondaryContext.Database.GetDbConnection();
        var secondaryConnectionString = secondaryContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Secondary connection settings are required.");
        if (secondaryConnection.State != ConnectionState.Closed)
        {
            throw new InvalidOperationException("The secondary module connection must be closed before coordination.");
        }

        var openedPrimaryConnection = primaryConnection.State == ConnectionState.Closed;
        if (openedPrimaryConnection)
        {
            await primaryContext.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            secondaryContext.Database.SetDbConnection(primaryConnection, contextOwnsConnection: false);
            await using var primaryTransaction = await primaryContext.Database.BeginTransactionAsync(cancellationToken);
            await using var secondaryTransaction = await secondaryContext.Database.UseTransactionAsync(
                primaryTransaction.GetDbTransaction(), cancellationToken);

            try
            {
                var result = await workflow(cancellationToken);
                if (shouldCommit is not null && !shouldCommit(result))
                {
                    await primaryTransaction.RollbackAsync(CancellationToken.None);
                    secondaryContext.ChangeTracker.Clear();
                    primaryContext.ChangeTracker.Clear();
                    return result;
                }
                await secondaryContext.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);
                await primaryContext.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);
                if (postSave is not null)
                {
                    // Store-generated values (for example PostgreSQL xmin) are now available to
                    // the callback. Accept the first EF save before the second one so already
                    // inserted entities are not submitted again. The transaction is still open;
                    // any failure below rolls it back and clears both trackers.
                    secondaryContext.ChangeTracker.AcceptAllChanges();
                    primaryContext.ChangeTracker.AcceptAllChanges();
                    result = await postSave(result, cancellationToken);
                    await secondaryContext.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);
                    await primaryContext.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);
                }
                await primaryTransaction.CommitAsync(cancellationToken);

                secondaryContext.ChangeTracker.AcceptAllChanges();
                primaryContext.ChangeTracker.AcceptAllChanges();
                return result;
            }
            catch
            {
                await primaryTransaction.RollbackAsync(CancellationToken.None);
                secondaryContext.ChangeTracker.Clear();
                primaryContext.ChangeTracker.Clear();
                throw;
            }
        }
        finally
        {
            // EF disposes an owned connection when replacing it; recreate it for subsequent work.
            secondaryContext.Database.SetDbConnection(new NpgsqlConnection(secondaryConnectionString), contextOwnsConnection: true);
            if (openedPrimaryConnection)
            {
                await primaryContext.Database.CloseConnectionAsync();
            }
        }
    }

    public static Task<TResult> ExecuteAsync<TResult>(
        DbContext primaryContext,
        DbContext secondaryContext,
        Func<CancellationToken, Task<TResult>> workflow,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(primaryContext, secondaryContext, workflow, postSave: null, cancellationToken);
}
