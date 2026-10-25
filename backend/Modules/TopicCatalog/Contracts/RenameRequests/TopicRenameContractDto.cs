namespace UGTM.Modules.TopicCatalog.Contracts.RenameRequests;

public sealed record TopicTitleSnapshotContractDto(
    Guid TopicId,
    Guid BatchId,
    Guid AdvisorId,
    string TitleVi,
    string? TitleEn);

public sealed record TopicTitleUpdateContractResultDto(
    bool IsSuccess,
    string? ErrorCode,
    string? ErrorMessage,
    string? OldTitleVi,
    string? NewTitleVi,
    string? OldTitleEn,
    string? NewTitleEn)
{
    public static TopicTitleUpdateContractResultDto Success(
        string oldTitleVi,
        string newTitleVi,
        string? oldTitleEn,
        string? newTitleEn)
        => new(true, null, null, oldTitleVi, newTitleVi, oldTitleEn, newTitleEn);

    public static TopicTitleUpdateContractResultDto Failure(string code, string message)
        => new(false, code, message, null, null, null, null);
}

public interface ITopicRenameContract
{
    Task<TopicTitleSnapshotContractDto?> GetTopicSnapshotAsync(
        Guid topicId,
        CancellationToken cancellationToken = default);

    Task<TopicTitleUpdateContractResultDto> UpdateTopicTitlesAsync(
        Guid topicId,
        string newTitleVi,
        string? newTitleEn,
        CancellationToken cancellationToken = default);
}
