using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

public sealed record GradingCriterionInput(string? Code, string? Name, decimal MaxScore);
public sealed record GradingCriterionDto(Guid Id, string Code, string Name, decimal MaxScore, int Order);
public sealed record GradingRubricDto(Guid BatchId, bool Locked, IReadOnlyList<GradingCriterionDto> Criteria);

public sealed class GradingRubricManager(DefenseCouncilDbContext db, IKltnBatchReader batches, ICurrentUser user)
{
    private static readonly Error Forbidden = Error.Forbidden("GradingRubric.Forbidden", "Không có quyền quản lý barem của đợt.");
    private static readonly Error Invalid = Error.Validation("GradingRubric.Invalid", "Barem cần 1–100 tiêu chí, mã duy nhất, điểm dương tối đa hai chữ số thập phân và tổng bằng 10.");
    private static readonly Error NotFound = Error.NotFound("GradingRubric.BatchNotFound", "Đợt KLTN không tồn tại.");
    private static readonly Error Locked = Error.Conflict("GradingRubric.Locked", "Barem đã được sử dụng để chấm và không thể thay đổi.");

    public async Task<Result<GradingRubricDto>> GetAsync(Guid batchId, CancellationToken token = default)
    {
        if (!CanRead()) return Result.Failure<GradingRubricDto>(Forbidden);
        if (batchId == Guid.Empty || !await batches.ExistsAsync(batchId, token)) return Result.Failure<GradingRubricDto>(NotFound);
        // Read both the lock flag and criteria under the same lock as writers.
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await GradingRubricLock.AcquireAsync(db, batchId, token);
        var result = await ReadAsync(batchId, token);
        await transaction.CommitAsync(token);
        return Result.Success(result);
    }

    public async Task<Result<GradingRubricDto>> ReplaceAsync(Guid batchId, IReadOnlyList<GradingCriterionInput>? inputs, CancellationToken token = default)
    {
        if (!CanManage()) return Result.Failure<GradingRubricDto>(Forbidden);
        if (batchId == Guid.Empty || !await batches.ExistsAsync(batchId, token)) return Result.Failure<GradingRubricDto>(NotFound);
        if (inputs is null || inputs.Count is < 1 or > 100 || inputs.Any(x => x is null ||
            string.IsNullOrWhiteSpace(x.Code) || x.Code.Trim().Length > 20 ||
            string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 250 ||
            x.MaxScore <= 0 || x.MaxScore > 10 || decimal.Round(x.MaxScore, 2) != x.MaxScore) ||
            inputs.Sum(x => x.MaxScore) != 10m ||
            inputs.Select(x => x.Code!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inputs.Count)
            return Result.Failure<GradingRubricDto>(Invalid);

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await GradingRubricLock.AcquireAsync(db, batchId, token);
        if (await IsUsedAsync(batchId, token)) return Result.Failure<GradingRubricDto>(Locked);

        var old = await db.GradingCriterias.Where(c => c.BatchId == batchId).ToListAsync(token);
        db.GradingCriterias.RemoveRange(old);
        await db.SaveChangesAsync(token); // Delete first to permit reusing criterion codes.
        var criteria = inputs.Select((input, index) => new GradingCriteria
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            CriteriaCode = input.Code!.Trim().ToUpperInvariant(),
            CriteriaName = input.Name!.Trim(),
            MaxScore = input.MaxScore,
            OrderIndex = index + 1
        }).ToList();
        db.GradingCriterias.AddRange(criteria);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Result.Success(ToDto(batchId, false, criteria));
    }

    private async Task<GradingRubricDto> ReadAsync(Guid batchId, CancellationToken token)
    {
        var criteria = await db.GradingCriterias.AsNoTracking().Where(c => c.BatchId == batchId)
            .OrderBy(c => c.OrderIndex).ThenBy(c => c.Id).ToListAsync(token);
        return ToDto(batchId, await IsUsedAsync(batchId, token), criteria);
    }

    private async Task<bool> IsUsedAsync(Guid batchId, CancellationToken token)
        => await db.Database.SqlQuery<Guid>($"SELECT batch_id AS \"Value\" FROM councils.grading_rubric_usage WHERE batch_id = {batchId}").AnyAsync(token);

    private static GradingRubricDto ToDto(Guid batchId, bool locked, IEnumerable<GradingCriteria> criteria)
        => new(batchId, locked, criteria.Select(c => new GradingCriterionDto(c.Id, c.CriteriaCode, c.CriteriaName, c.MaxScore, c.OrderIndex)).ToList());

    private bool CanManage() => user.IsAuthenticated && user.UserId.HasValue && user.Roles.Any(role =>
        role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || role.Equals("Faculty", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("AcademicAffairs", StringComparison.OrdinalIgnoreCase));
    private bool CanRead() => CanManage() || (user.IsAuthenticated && user.UserId.HasValue && user.Roles.Any(role => role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase)));
}
