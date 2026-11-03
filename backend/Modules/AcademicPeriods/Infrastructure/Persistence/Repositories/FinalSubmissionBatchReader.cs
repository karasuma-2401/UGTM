using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.AcademicPeriods.Contracts.FinalSubmissions;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

internal sealed class FinalSubmissionBatchReader(AcademicPeriodsDbContext db) : IFinalSubmissionBatchReader
{
    public Task<FinalSubmissionBatchContractDto?> GetAsync(Guid batchId, CancellationToken token = default)
        => ReadAsync(batchId, false, token);
    public Task<FinalSubmissionBatchContractDto?> LockAsync(Guid batchId, CancellationToken token = default)
        => ReadAsync(batchId, true, token);

    private async Task<FinalSubmissionBatchContractDto?> ReadAsync(Guid id, bool locked, CancellationToken token)
    {
        if (locked && db.Database.CurrentTransaction is null) throw new InvalidOperationException("Shared transaction required.");
        var batch = locked
            ? await db.KltnBatches.FromSqlInterpolated($"SELECT b.*, b.xmin FROM academic.kltn_batch b WHERE b.id = {id} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(token)
            : await db.KltnBatches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, token);
        if (batch is null) return null;
        var stages = locked
            ? await db.Stages.FromSqlInterpolated($"SELECT s.* FROM academic.stage s WHERE s.batch_id = {id} ORDER BY s.id FOR SHARE").AsNoTracking().ToListAsync(token)
            : await db.Stages.AsNoTracking().Where(s => s.BatchId == id).ToListAsync(token);
        var matches = stages.Where(s => s.StageCode == "ARCHIVAL_AND_PUBLICATION").ToArray();
        DateTime? opens = null;
        DateTime? deadline = null;
        if (matches.Length == 1)
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(batch.TimeZoneId);
                opens = TimeZoneInfo.ConvertTimeToUtc(matches[0].StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
                deadline = matches[0].DeadlineAtUtc;
                if (deadline < opens) opens = deadline = null;
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { }
        }
        var json = JsonSerializer.Serialize(new { batch.Id, batch.TimeZoneId, Opens = opens, Deadline = deadline });
        return new(id, batch.Status.ToString(), opens, deadline,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant());
    }
}
