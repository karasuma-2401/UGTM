using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Contracts.TopicRegistrations;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

public sealed class ThesisRegistrationContract(ThesisManagementDbContext db) : IThesisRegistrationContract
{
    public async Task<IReadOnlyDictionary<Guid, TopicRegistrationContractDto>> GetRegistrationsByTopicIdsAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default)
    {
        var topicIdList = topicIds.Distinct().ToList();
        if (topicIdList.Count == 0)
        {
            return new Dictionary<Guid, TopicRegistrationContractDto>();
        }

        var groups = await db.StudentGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .Where(g => topicIdList.Contains(g.TopicId))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, TopicRegistrationContractDto>();
        foreach (var group in groups)
        {
            var members = group.Members
                .Select(m => new TopicMemberContractDto(m.StudentId, m.BatchId, m.JoinedAt))
                .ToList();

            result[group.TopicId] = new TopicRegistrationContractDto(
                group.Id,
                group.TopicId,
                group.Status.ToString(),
                group.CreatedAt,
                members);
        }

        return result;
    }

    public async Task<TopicRegistrationContractDto?> GetRegistrationByTopicIdAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var group = await db.StudentGroups
            .AsNoTracking()
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.TopicId == topicId, cancellationToken);

        if (group is null)
        {
            return null;
        }

        var members = group.Members
            .Select(m => new TopicMemberContractDto(m.StudentId, m.BatchId, m.JoinedAt))
            .ToList();

        return new TopicRegistrationContractDto(
            group.Id,
            group.TopicId,
            group.Status.ToString(),
            group.CreatedAt,
            members);
    }
}
