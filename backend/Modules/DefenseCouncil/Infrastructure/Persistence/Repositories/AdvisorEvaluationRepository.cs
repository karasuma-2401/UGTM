using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class AdvisorEvaluationRepository(DefenseCouncilDbContext context) : IAdvisorEvaluationRepository
{
    public Task<AdvisorEvaluation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.AdvisorEvaluations.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

    public Task<AdvisorEvaluation?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        => context.AdvisorEvaluations.SingleOrDefaultAsync(value => value.GroupId == groupId, cancellationToken);

    public void Add(AdvisorEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        context.AdvisorEvaluations.Add(evaluation);
    }

    public async Task<IReadOnlyList<GradingCriteria>> GetGradingCriteriaByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
        => await context.GradingCriterias
            .Where(g => g.BatchId == batchId)
            .OrderBy(g => g.OrderIndex)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EvaluationDetail>> GetEvaluationDetailsByEvaluationIdAsync(Guid evaluationId, CancellationToken cancellationToken = default)
        => await context.EvaluationDetails
            .Include(d => d.Criteria)
            .Where(d => d.EvaluationId == evaluationId)
            .ToListAsync(cancellationToken);

    public async Task SaveEvaluationDetailsAsync(Guid evaluationId, IEnumerable<EvaluationDetail> details, CancellationToken cancellationToken = default)
    {
        var existing = await context.EvaluationDetails
            .Where(d => d.EvaluationId == evaluationId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            context.EvaluationDetails.RemoveRange(existing);
        }

        foreach (var detail in details)
        {
            detail.EvaluationId = evaluationId;
            await context.EvaluationDetails.AddAsync(detail, cancellationToken);
        }
    }
}
