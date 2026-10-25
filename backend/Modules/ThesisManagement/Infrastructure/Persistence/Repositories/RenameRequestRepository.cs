using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

public sealed class RenameRequestRepository(ThesisManagementDbContext db) : IRenameRequestRepository
{
    private static readonly RenameRequestStatus[] ActiveStatuses =
    [
        RenameRequestStatus.Created,
        RenameRequestStatus.AdvisorConfirmed,
        RenameRequestStatus.FacultyApproved
    ];

    public void Add(RenameRequest request)
        => db.RenameRequests.Add(request);

    public Task<RenameRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.RenameRequests
            .Include(r => r.Group)
            .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RenameRequest>> GetByGroupIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
        => await db.RenameRequests
            .AsNoTracking()
            .Where(r => r.GroupId == groupId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> HasActiveRequestForGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
        => db.RenameRequests
            .AsNoTracking()
            .AnyAsync(r => r.GroupId == groupId && ActiveStatuses.Contains(r.Status), cancellationToken);

    public async Task<IReadOnlyList<RenameRequest>> ListAsync(
        Guid? groupId,
        RenameRequestStatus? status,
        IReadOnlyCollection<Guid>? topicIdsFilter,
        CancellationToken cancellationToken = default)
    {
        var query = db.RenameRequests
            .AsNoTracking()
            .Include(r => r.Group)
            .ThenInclude(g => g.Members)
            .AsQueryable();

        if (groupId.HasValue && groupId.Value != Guid.Empty)
        {
            query = query.Where(r => r.GroupId == groupId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (topicIdsFilter is not null)
        {
            query = query.Where(r => topicIdsFilter.Contains(r.Group.TopicId));
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
