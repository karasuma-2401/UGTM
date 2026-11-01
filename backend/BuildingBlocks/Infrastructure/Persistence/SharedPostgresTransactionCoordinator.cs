using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace UGTM.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Executes a callback and persists two module contexts through one PostgreSQL transaction.
/// Contexts continue to enforce their own schema write ownership.
/// </summary>
public static class SharedPostgresTransactionCoordinator
{
    public static async Task<TResult> ExecuteAsync<TResult>(
        DbContext primaryContext,
        DbContext secondaryContext,
        Func<CancellationToken, Task<TResult>> workflow,
        Func<TResult, CancellationToken, Task<TResult>>? postSave,
        CancellationToken cancellationToken = default)
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
            secondaryContext.Database.SetDbConnection(secondaryConnection, contextOwnsConnection: true);
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
