namespace UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;

/// <summary>
/// Runs an advisor-evaluation workflow callback in one PostgreSQL transaction shared by
/// DefenseCouncil and ThesisManagement. The coordinator saves both module contexts only when
/// the callback succeeds and rolls every pending change back when either save fails.
/// </summary>
public interface IAdvisorWorkflowTransactionCoordinator
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> workflow,
        CancellationToken cancellationToken = default);
}
