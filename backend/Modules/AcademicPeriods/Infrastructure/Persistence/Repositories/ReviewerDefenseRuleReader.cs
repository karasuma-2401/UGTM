using Microsoft.EntityFrameworkCore;
using UGTM.Modules.AcademicPeriods.Contracts.ReviewerEvaluations;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerDefenseRuleReader(AcademicPeriodsDbContext context) : IReviewerDefenseRuleReader
{
    public async Task<string?> GetBatchStatusAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await context.KltnBatches.AsNoTracking().Where(value => value.Id == batchId)
            .Select(value => new { value.Status }).SingleOrDefaultAsync(cancellationToken);
        return batch?.Status.ToString();
    }

    public Task<ReviewerDefenseRuleContractDto?> GetActiveAsync(Guid batchId, CancellationToken cancellationToken = default)
        => ReadAsync(batchId, false, cancellationToken);
    public Task<ReviewerDefenseRuleContractDto?> LockActiveAsync(Guid batchId, CancellationToken cancellationToken = default)
        => ReadAsync(batchId, true, cancellationToken);

    private async Task<ReviewerDefenseRuleContractDto?> ReadAsync(Guid batchId, bool locked, CancellationToken token)
    {
        if (locked && context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A shared reviewer transaction is required.");
        var batch = await context.KltnBatches.AsNoTracking().Where(value => value.Id == batchId)
            .Select(value => new { value.Id, value.Status }).SingleOrDefaultAsync(token);
        if (batch is null) return null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var activeIds = await context.ScoreWeightConfigs.AsNoTracking()
                .Where(value => value.BatchId == batchId && value.Status == ConfigurationVersionStatus.Active)
                .OrderBy(value => value.Id).Select(value => value.Id).Take(2).ToArrayAsync(token);
            if (activeIds.Length != 1)
                return new(batchId, batch.Status.ToString(), null, null,
                    activeIds.Length == 0 ? ReviewerDefenseRuleReadOutcome.ConfigurationMissing : ReviewerDefenseRuleReadOutcome.ConfigurationInvalid, []);
            var id = activeIds[0];
            var config = locked
                ? await context.ScoreWeightConfigs.FromSqlInterpolated($"""
                    SELECT c.*, c.xmin FROM academic.score_weight_config c WHERE c.id = {id} FOR SHARE
                    """).AsNoTracking().SingleOrDefaultAsync(token)
                : await context.ScoreWeightConfigs.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, token);
            // If a publisher retired this row while FOR SHARE waited, start a new SQL statement.
            // Never keep the old Active projection as proof after the wait.
            if (config is null || config.BatchId != batchId || config.Status != ConfigurationVersionStatus.Active) continue;
            // Keep applicability as data so malformed historical enum text becomes an Invalid
            // resolution rather than throwing in EF's enum converter before the resolver runs.
            var rows = await context.Database.SqlQuery<ReviewerDefenseRuleComponentContractDto>($"""
                SELECT c.id, c.applicability, c.minimum_score
                FROM academic.score_component_config c WHERE c.config_id = {id} AND c.source = 'Reviewer'
                ORDER BY c.order_index, c.id
                """).ToListAsync(token);
            return new(batchId, batch.Status.ToString(), id, config.VersionNo, ReviewerDefenseRuleReadOutcome.Succeeded,
                rows);
        }
        throw new ReviewerDefenseRuleConcurrencyException();
    }
}
