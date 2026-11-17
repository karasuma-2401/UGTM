using Microsoft.EntityFrameworkCore;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

internal static class GradingRubricLock
{
    // Shared by rubric management, score writers, and the database insert trigger.
    public static async Task AcquireAsync(DefenseCouncilDbContext db, Guid batchId, CancellationToken token)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A transaction is required to lock the grading rubric.");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({batchId.ToString()}, 8753201))", token);
    }
}
