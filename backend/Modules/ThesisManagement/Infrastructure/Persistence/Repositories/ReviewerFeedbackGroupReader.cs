using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerFeedbackGroupReader(ThesisManagementDbContext context) : IReviewerFeedbackGroupReader
{
    public async Task<ReviewerFeedbackGroupContractDto?> GetAsync(Guid groupId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var group = await context.StudentGroups.AsNoTracking().Where(value => value.Id == groupId)
            .Select(value => new { value.Id, value.TopicId }).SingleOrDefaultAsync(cancellationToken);
        if (group is null) return null;
        var members = await context.StudentGroupMembers.AsNoTracking().Where(value => value.GroupId == groupId)
            .Select(value => new { value.BatchId, value.StudentId, value.ConfirmationStatus }).ToListAsync(cancellationToken);
        var batches = members.Select(value => value.BatchId).Distinct().ToArray();
        var valid = group.TopicId != Guid.Empty && batches.Length == 1 && batches[0] != Guid.Empty;
        return new(group.Id, group.TopicId, valid ? batches[0] : null, valid,
            actorId != Guid.Empty && members.Any(value => value.StudentId == actorId && value.ConfirmationStatus == MemberConfirmationStatus.Confirmed));
    }
}
