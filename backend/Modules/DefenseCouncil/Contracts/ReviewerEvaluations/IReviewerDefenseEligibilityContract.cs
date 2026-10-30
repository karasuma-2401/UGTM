namespace UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;

public enum ReviewerDefenseEligibilityReason
{
    ContextInvalid, GroupNotReady, AssignmentMissing, LegacyAssignment, AdvisorConflict, BindingInvalid,
    EvaluationMissing, LegacyEvaluation, EvaluationNotSubmitted, EvaluationExcluded,
    ConfigurationInvalid, ConfigurationMissing, ConfigurationAmbiguous, DefenseRejected, ScoreBelowMinimum
}

public sealed record ReviewerDefenseEligibilityContractDto(Guid GroupId, Guid? BatchId, string? BatchStatus,
    Guid? AssignmentId, Guid? EvaluationId, uint? EvaluationVersion, Guid? ScoreConfigurationId,
    int? ScoreConfigurationVersionNo, decimal? MinimumReviewerScore, bool CanProceed,
    ReviewerDefenseEligibilityReason? ReasonCode);

// Implementation is CP3; no stub is registered in the foundation checkpoint.
public interface IReviewerDefenseEligibilityContract
{
    Task<ReviewerDefenseEligibilityContractDto?> GetAsync(Guid groupId, CancellationToken cancellationToken = default);
    /// <summary>Use inside the scheduling writer's coordinator transaction and retain locks to commit.</summary>
    Task<ReviewerDefenseEligibilityContractDto?> LockForSchedulingAsync(Guid groupId, CancellationToken cancellationToken = default);
}
