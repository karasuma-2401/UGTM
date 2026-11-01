using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerEvaluationRepository(DefenseCouncilDbContext context) : IReviewerEvaluationRepository
{
    public Task<ReviewerEvaluation?> GetByAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
        => context.ReviewerEvaluations.AsNoTracking().SingleOrDefaultAsync(value => value.AssignmentId == assignmentId, cancellationToken);

    public async Task<ReviewerEvaluation?> LockByAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("A shared reviewer transaction is required.");
        var existing = context.ChangeTracker.Entries<ReviewerEvaluation>().SingleOrDefault(value => value.Entity.AssignmentId == assignmentId);
        if (existing is not null && existing.State != EntityState.Unchanged)
            throw new InvalidOperationException("Acquire the evaluation lock before changing it.");
        var value = await context.ReviewerEvaluations.FromSqlInterpolated($"""
            SELECT e.*, e.xmin FROM councils.reviewer_evaluation e WHERE e.assignment_id = {assignmentId} FOR UPDATE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (existing is not null) existing.State = EntityState.Detached;
        if (value is not null) context.ReviewerEvaluations.Attach(value);
        return value;
    }

    public void Add(ReviewerEvaluation evaluation) => context.ReviewerEvaluations.Add(evaluation);
}
