namespace UGTM.Modules.DefenseCouncil.Contracts.ScoreSummaries;

public sealed record ScoreSummaryTransactionResult<T>(T Value, bool ShouldCommit)
{
    public static ScoreSummaryTransactionResult<T> Commit(T value) => new(value, true);
    public static ScoreSummaryTransactionResult<T> Abort(T value) => new(value, false);
}
public interface IScoreSummaryWorkflowTransactionCoordinator
{
    Task<T> ExecuteWithPostSaveAsync<T>(Func<CancellationToken, Task<ScoreSummaryTransactionResult<T>>> workflow,
        Func<T, CancellationToken, Task<T>> postSave, CancellationToken token = default);
}
