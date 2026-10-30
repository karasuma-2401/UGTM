namespace UGTM.Modules.AcademicPeriods.Contracts.ReviewerEvaluations;

public enum ReviewerDefenseRuleReadOutcome { Succeeded, ConfigurationMissing, ConfigurationInvalid }
public sealed class ReviewerDefenseRuleConcurrencyException()
    : Exception("The active reviewer rule changed while acquiring its lock.");
public sealed record ReviewerDefenseRuleComponentContractDto(Guid Id, string Applicability, decimal? MinimumScore);
public sealed record ReviewerDefenseRuleContractDto(Guid BatchId, string BatchStatus, Guid? ConfigurationId,
    int? ConfigurationVersionNo, ReviewerDefenseRuleReadOutcome Outcome,
    IReadOnlyList<ReviewerDefenseRuleComponentContractDto> Components);

public interface IReviewerDefenseRuleReader
{
    Task<ReviewerDefenseRuleContractDto?> GetActiveAsync(Guid batchId, CancellationToken cancellationToken = default);
    /// <summary>Requires the shared transaction; retains the current Active config lock until caller commit.</summary>
    Task<ReviewerDefenseRuleContractDto?> LockActiveAsync(Guid batchId, CancellationToken cancellationToken = default);
}
