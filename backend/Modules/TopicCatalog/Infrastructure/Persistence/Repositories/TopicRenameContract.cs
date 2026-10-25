using Microsoft.EntityFrameworkCore;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

internal sealed class TopicRenameContract(TopicCatalogDbContext context) : ITopicRenameContract
{
    public Task<TopicTitleSnapshotContractDto?> GetTopicSnapshotAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
        => context.Topics
            .AsNoTracking()
            .Where(topic => topic.Id == topicId)
            .Select(topic => new TopicTitleSnapshotContractDto(
                topic.Id,
                topic.BatchId,
                topic.AdvisorId,
                topic.TitleVi,
                topic.TitleEn))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<TopicTitleUpdateContractResultDto> UpdateTopicTitlesAsync(
        Guid topicId,
        string newTitleVi,
        string? newTitleEn,
        CancellationToken cancellationToken = default)
    {
        var topic = await context.Topics.FirstOrDefaultAsync(t => t.Id == topicId, cancellationToken);
        if (topic is null)
        {
            return TopicTitleUpdateContractResultDto.Failure("Topic.NotFound", "Không tìm thấy đề tài.");
        }

        var trimmedVi = newTitleVi.Trim();
        var trimmedEn = string.IsNullOrWhiteSpace(newTitleEn) ? null : newTitleEn.Trim();

        var isDuplicate = await context.Topics
            .AnyAsync(
                t => t.Id != topicId
                     && t.BatchId == topic.BatchId
                     && t.AdvisorId == topic.AdvisorId
                     && t.TitleVi.ToLower() == trimmedVi.ToLower(),
                cancellationToken);

        if (isDuplicate)
        {
            return TopicTitleUpdateContractResultDto.Failure(
                "Topic.DuplicateTitle",
                "Tên tiếng Việt của đề tài bị trùng với đề tài khác của cùng giảng viên trong đợt.");
        }

        var oldTitleVi = topic.TitleVi;
        var oldTitleEn = topic.TitleEn;

        topic.TitleVi = trimmedVi;
        topic.TitleEn = trimmedEn;

        await context.SaveChangesAsync(cancellationToken);

        return TopicTitleUpdateContractResultDto.Success(
            oldTitleVi,
            topic.TitleVi,
            oldTitleEn,
            topic.TitleEn);
    }
}
