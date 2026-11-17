namespace UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;

public sealed record ReviewerAssignmentTransactionResult<T>(T Value, bool ShouldCommit)
{
    public static ReviewerAssignmentTransactionResult<T> Commit(T value) => new(value, true);
    public static ReviewerAssignmentTransactionResult<T> Abort(T value) => new(value, false);
}

/// <summary>
/// Owns one PostgreSQL transaction across the workflow's module contracts. Abort rolls back all
/// tracked writes, even when the callback returns normally. Callbacks must not perform external IO.
/// </summary>
public interface IReviewerAssignmentTransactionCoordinator
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<ReviewerAssignmentTransactionResult<T>>> workflow,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteWithPostSaveAsync<T>(Func<CancellationToken, Task<ReviewerAssignmentTransactionResult<T>>> workflow,
        Func<T, CancellationToken, Task<T>> postSave, CancellationToken cancellationToken = default);
}
