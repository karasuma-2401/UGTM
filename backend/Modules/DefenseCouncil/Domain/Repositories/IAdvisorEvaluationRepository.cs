using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Domain.Repositories;

public interface IAdvisorEvaluationRepository
{
    Task<AdvisorEvaluation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdvisorEvaluation?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default);

    void Add(AdvisorEvaluation evaluation);

    Task<IReadOnlyList<GradingCriteria>> GetGradingCriteriaByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvaluationDetail>> GetEvaluationDetailsByEvaluationIdAsync(Guid evaluationId, CancellationToken cancellationToken = default);

    Task SaveEvaluationDetailsAsync(Guid evaluationId, IEnumerable<EvaluationDetail> details, CancellationToken cancellationToken = default);
}
