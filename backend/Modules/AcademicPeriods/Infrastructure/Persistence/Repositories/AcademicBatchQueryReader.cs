using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.AcademicPeriods.Application.Batches.Dtos;
using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

public sealed class AcademicBatchQueryReader(AcademicPeriodsDbContext db) : IAcademicBatchQueryReader
{
    public async Task<PagedResult<KltnBatchSummaryDto>> GetBatchesAsync(
        string? academicYear,
        int? semester,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = db.KltnBatches.AsNoTracking();
        if (academicYear is not null)
        {
            query = query.Where(batch => batch.AcademicYear == academicYear);
        }

        if (semester.HasValue)
        {
            query = query.Where(batch => batch.Semester == semester.Value);
        }

        if (status is not null && Enum.TryParse<BatchStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(batch => batch.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(batch => batch.StartDate)
            .ThenByDescending(batch => batch.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(batch => new KltnBatchSummaryDto(
            batch.Id,
            batch.BatchName,
            batch.AcademicYear,
            batch.Semester,
            batch.StartDate,
            batch.EndDate,
            batch.TimeZoneId,
            batch.Status.ToString(),
            batch.CreatedAt,
            batch.UpdatedAt)).ToArray();

        return new PagedResult<KltnBatchSummaryDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<KltnBatchDetailDto?> GetByIdAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.KltnBatches
            .AsNoTracking()
            .Include(entity => entity.Stages)
            .SingleOrDefaultAsync(entity => entity.Id == batchId, cancellationToken);

        return batch is null ? null : AcademicBatchMapper.ToDetail(batch);
    }
}
