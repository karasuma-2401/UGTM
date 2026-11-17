namespace UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;

public sealed record FinalSubmissionTransactionResult<T>(T Value, bool ShouldCommit)
{
    public static FinalSubmissionTransactionResult<T> Commit(T value) => new(value, true);
    public static FinalSubmissionTransactionResult<T> Abort(T value) => new(value, false);
}

public interface IFinalSubmissionWorkflowTransactionCoordinator
{
    Task<T> ExecuteWithPostSaveAsync<T>(
        Func<CancellationToken, Task<FinalSubmissionTransactionResult<T>>> workflow,
        Func<T, CancellationToken, Task<T>> postSave, CancellationToken token = default);
}
