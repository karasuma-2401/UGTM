using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Tests.Fakes;

public sealed class FakeTopicRepository : ITopicRepository
{
    private readonly Dictionary<Guid, Topic> _topics = [];

    public IReadOnlyCollection<Topic> All => _topics.Values;

    public void Add(Topic topic)
    {
        _topics[topic.Id] = topic;
    }

    public void Remove(Topic topic)
    {
        _topics.Remove(topic.Id);
    }

    public Task<Topic?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _topics.TryGetValue(id, out var topic);
        return Task.FromResult(topic);
    }

    public Task<bool> ExistsByAdvisorAndTitleAsync(
        Guid batchId,
        Guid advisorId,
        string titleVi,
        Guid? excludeTopicId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = titleVi.Trim();
        var exists = _topics.Values.Any(t =>
            t.BatchId == batchId
            && t.AdvisorId == advisorId
            && (!excludeTopicId.HasValue || t.Id != excludeTopicId.Value)
            && string.Equals(t.TitleVi.Trim(), normalized, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<Topic>> GetByBatchAsync(
        Guid batchId,
        IReadOnlyCollection<Guid>? topicIds = null,
        CancellationToken cancellationToken = default)
    {
        var query = _topics.Values.Where(t => t.BatchId == batchId);
        if (topicIds is { Count: > 0 })
        {
            query = query.Where(t => topicIds.Contains(t.Id));
        }

        IReadOnlyList<Topic> list = query
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .ToList();

        return Task.FromResult(list);
    }
}

public sealed class FakeTopicUnitOfWork : ITopicUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(1);
    }
}

public sealed class FakeKltnBatchReader(params Guid[] existingBatchIds) : IKltnBatchReader
{
    public Task<BatchFinalReportSubmissionWindowContractDto?> GetFinalReportSubmissionWindowAsync(
        Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    private readonly HashSet<Guid> _batches = new(existingBatchIds);

    public void AddBatch(Guid batchId) => _batches.Add(batchId);

    public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default)
        => Task.FromResult(_batches.Contains(batchId));

    public Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (!_batches.Contains(batchId))
        {
            return Task.FromResult<BatchRegistrationWindowContractDto?>(null);
        }

        return Task.FromResult<BatchRegistrationWindowContractDto?>(
            new BatchRegistrationWindowContractDto(batchId, "Open", true, nowUtc.AddDays(7)));
    }

    public Task<BatchTopicPreparationWindowContractDto?> GetTopicPreparationWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (!_batches.Contains(batchId))
        {
            return Task.FromResult<BatchTopicPreparationWindowContractDto?>(null);
        }

        return Task.FromResult<BatchTopicPreparationWindowContractDto?>(
            new BatchTopicPreparationWindowContractDto(batchId, "Open", true, nowUtc.AddDays(7)));
    }

    public Task<BatchRenameRequestWindowContractDto?> GetRenameRequestWindowAsync(
        Guid batchId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (!_batches.Contains(batchId))
        {
            return Task.FromResult<BatchRenameRequestWindowContractDto?>(null);
        }

        return Task.FromResult<BatchRenameRequestWindowContractDto?>(
            new BatchRenameRequestWindowContractDto(
                batchId,
                "Open",
                true,
                DateOnly.FromDateTime(nowUtc.AddDays(-7)),
                nowUtc.AddDays(7)));
    }
}
