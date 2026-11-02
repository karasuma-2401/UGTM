using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Contracts.FinalSubmissions;
using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class LockedDefenseProofReader(DefenseCouncilDbContext db) : ILockedDefenseProofReader
{
    public Task<LockedDefenseProofContractDto> GetAsync(Guid groupId, CancellationToken token = default) => ReadAsync(groupId, false, token);
    public Task<LockedDefenseProofContractDto> LockAsync(Guid groupId, CancellationToken token = default) => ReadAsync(groupId, true, token);
    private async Task<LockedDefenseProofContractDto> ReadAsync(Guid id, bool locked, CancellationToken token)
    {
        if (locked && db.Database.CurrentTransaction is null) throw new InvalidOperationException("Shared transaction required.");
        var links = await db.CouncilAssignments.AsNoTracking().Where(a => a.GroupId == id).ToListAsync(token);
        LockedDefenseProofContractDto Failed(LockedDefenseOutcome outcome) => new(outcome, id, Guid.Empty, null, null, null, null, null, null, null);
        if (links.Count == 0) return Failed(LockedDefenseOutcome.Missing);
        if (links.Count != 1) return Failed(LockedDefenseOutcome.ContextInvalid);
        var councilId = links[0].CouncilId;
        var council = locked
            ? await db.Councils.FromSqlInterpolated($"SELECT c.* FROM councils.council c WHERE c.id={councilId} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(token)
            : await db.Councils.AsNoTracking().SingleOrDefaultAsync(c => c.Id == councilId, token);
        if (locked)
        {
            links = await db.CouncilAssignments.FromSqlInterpolated($"SELECT a.* FROM councils.council_assignment a WHERE a.group_id={id} ORDER BY a.id FOR SHARE").AsNoTracking().ToListAsync(token);
            await db.CouncilMembers.FromSqlInterpolated($"SELECT m.* FROM councils.council_member m WHERE m.council_id={councilId} ORDER BY m.id FOR SHARE").AsNoTracking().ToListAsync(token);
        }
        if (council is null || links.Count != 1 || links[0].CouncilId != councilId || council.BatchId == Guid.Empty)
            return Failed(LockedDefenseOutcome.ContextInvalid);
        var assignmentId = links[0].Id;
        var minute = locked
            ? await db.CouncilMinutes.FromSqlInterpolated($"SELECT m.*, m.xmin FROM councils.council_minute m WHERE m.council_assignment_id={assignmentId} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(token)
            : await db.CouncilMinutes.AsNoTracking().SingleOrDefaultAsync(m => m.CouncilAssignmentId == assignmentId, token);
        if (minute is null) return Failed(LockedDefenseOutcome.Missing);
        if (minute.LockStatus != CouncilMinuteLockStatus.Locked) return Failed(LockedDefenseOutcome.NotLocked);
        if (minute.LockedBy is null || minute.LockedBy == Guid.Empty || minute.LockedAt is null || minute.Version == 0)
            return Failed(LockedDefenseOutcome.ContextInvalid);
        var canonical = JsonSerializer.Serialize(new { councilId, assignmentId, minute.Id, minute.Version, minute.LockedBy, minute.LockedAt });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new(LockedDefenseOutcome.Succeeded, id, council.BatchId, councilId, assignmentId, minute.Id,
            minute.Version, minute.LockedBy, minute.LockedAt, hash);
    }
}
