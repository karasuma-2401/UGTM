using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerAssignmentRepository(DefenseCouncilDbContext context) : IReviewerAssignmentRepository
{
    public Task<ReviewerAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.ReviewerAssignments.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

    public Task<ReviewerAssignment?> GetActiveByGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
        => context.ReviewerAssignments.AsNoTracking().SingleOrDefaultAsync(value => value.GroupId == groupId && value.IsActive,
            cancellationToken);

    public async Task<IReadOnlyList<ReviewerAssignment>> GetActiveByReviewerAndFileAsync(Guid reviewerId, Guid fileId,
        CancellationToken cancellationToken = default)
        => await context.ReviewerAssignments.AsNoTracking().Where(value => value.ReviewerId == reviewerId
            && value.SubmissionFileId == fileId && value.IsActive && !value.IsLegacy)
            .OrderBy(value => value.Id).ToListAsync(cancellationToken);

    public async Task<ReviewerAssignment?> LockByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("A shared reviewer transaction is required.");
        var existing = context.ChangeTracker.Entries<ReviewerAssignment>().SingleOrDefault(value => value.Entity.Id == id);
        if (existing is not null && existing.State != EntityState.Unchanged)
        {
            throw new InvalidOperationException("Acquire the assignment lock before making changes.");
        }
        var value = await context.ReviewerAssignments.FromSqlInterpolated($"""
            SELECT a.*, a.xmin FROM councils.reviewer_assignment a WHERE a.id = {id} FOR UPDATE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (existing is not null) existing.State = EntityState.Detached;
        if (value is not null) context.ReviewerAssignments.Attach(value);
        return value;
    }

    public Task<bool> HasEvaluationAsync(Guid assignmentId, CancellationToken cancellationToken = default)
        => context.ReviewerEvaluations.AsNoTracking().AnyAsync(value => value.AssignmentId == assignmentId, cancellationToken);

    public void Add(ReviewerAssignment assignment) => context.ReviewerAssignments.Add(assignment);
}
