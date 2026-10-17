using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Contracts.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

public sealed class ThesisRegistrationContract : IThesisRegistrationContract
{
    private readonly ThesisManagementDbContext? _db;
    private readonly IReadOnlyList<StudentGroup>? _inMemoryGroups;

    public ThesisRegistrationContract(ThesisManagementDbContext db)
    {
        _db = db;
    }

    private ThesisRegistrationContract(IReadOnlyList<StudentGroup> inMemoryGroups)
    {
        _inMemoryGroups = inMemoryGroups;
    }

    public static ThesisRegistrationContract CreateInMemory(IEnumerable<StudentGroup> groups)
        => new(groups.ToList());

    public async Task<IReadOnlyDictionary<Guid, TopicRegistrationContractDto>> GetRegistrationsByTopicIdsAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default)
    {
        var topicIdList = topicIds.Distinct().ToList();
        if (topicIdList.Count == 0)
        {
            return new Dictionary<Guid, TopicRegistrationContractDto>();
        }

        var query = QueryGroups()
            .Where(g => topicIdList.Contains(g.TopicId) && g.Status != StudentGroupStatus.Stopped)
            .OrderBy(g => g.CreatedAt)
            .ThenBy(g => g.Id);

        var groups = query is IAsyncEnumerable<StudentGroup>
            ? await query.ToListAsync(cancellationToken)
            : query.ToList();

        var result = new Dictionary<Guid, TopicRegistrationContractDto>();
        foreach (var group in groups)
        {
            var members = group.Members
                .OrderBy(m => m.JoinedAt)
                .ThenBy(m => m.StudentId)
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
        var query = QueryGroups()
            .Where(g => g.TopicId == topicId && g.Status != StudentGroupStatus.Stopped)
            .OrderByDescending(g => g.CreatedAt)
            .ThenByDescending(g => g.Id);

        var group = query is IAsyncEnumerable<StudentGroup>
            ? await query.FirstOrDefaultAsync(cancellationToken)
            : query.FirstOrDefault();

        if (group is null)
        {
            return null;
        }

        var members = group.Members
            .OrderBy(m => m.JoinedAt)
            .ThenBy(m => m.StudentId)
            .Select(m => new TopicMemberContractDto(m.StudentId, m.BatchId, m.JoinedAt))
            .ToList();

        return new TopicRegistrationContractDto(
            group.Id,
            group.TopicId,
            group.Status.ToString(),
            group.CreatedAt,
            members);
    }

    private IQueryable<StudentGroup> QueryGroups()
        => _inMemoryGroups is not null
            ? _inMemoryGroups.AsQueryable()
            : _db!.StudentGroups.AsNoTracking().Include(g => g.Members);
}
