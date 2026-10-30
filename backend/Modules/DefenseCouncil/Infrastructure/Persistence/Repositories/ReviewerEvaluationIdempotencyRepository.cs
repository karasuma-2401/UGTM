using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerEvaluationIdempotencyRepository(DefenseCouncilDbContext context)
    : IReviewerEvaluationIdempotencyRepository
{
    public async Task LockRequestAsync(Guid actorId, string operation, Guid key, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("A shared reviewer transaction is required.");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"reviewer-evaluation:{actorId:N}:{operation}:{key:N}"));
        var lockId = BinaryPrimitives.ReadInt64BigEndian(hash);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", cancellationToken);
    }

    public Task<ReviewerEvaluationIdempotencyRecord?> GetAsync(Guid actorId, string operation, Guid key,
        CancellationToken cancellationToken = default)
        => context.ReviewerEvaluationIdempotencyRecords.SingleOrDefaultAsync(value => value.ActorId == actorId
            && value.Operation == operation && value.IdempotencyKey == key, cancellationToken);

    public void Add(ReviewerEvaluationIdempotencyRecord record) => context.ReviewerEvaluationIdempotencyRecords.Add(record);
}
