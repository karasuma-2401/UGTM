using UGTM.Modules.TopicCatalog.Domain.Entities;

namespace UGTM.Modules.TopicCatalog.Domain.Repositories;

public interface ITopicRepository
{
    Task<Topic?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(Topic topic);
    void Remove(Topic topic);

    Task<bool> ExistsByAdvisorAndTitleAsync(
        Guid batchId,
        Guid advisorId,
        string titleVi,
        Guid? excludeTopicId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Topic>> GetByBatchAsync(
        Guid batchId,
        IReadOnlyCollection<Guid>? topicIds = null,
        CancellationToken cancellationToken = default);
}
