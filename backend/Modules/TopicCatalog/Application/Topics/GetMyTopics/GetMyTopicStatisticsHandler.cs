using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;

public sealed record TopicStatisticsDto(int Total, int Submitted, int NeedRevision);
public interface ITopicStatisticsReader
{
    Task<TopicStatisticsDto> GetAsync(Guid advisorId, Guid? batchId, CancellationToken cancellationToken);
}

public sealed class GetMyTopicStatisticsHandler(ITopicStatisticsReader reader, ICurrentUser currentUser)
{
    public async Task<Result<TopicStatisticsDto>> HandleAsync(Guid? batchId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } advisorId || advisorId == Guid.Empty)
            return Result.Failure<TopicStatisticsDto>(Error.Unauthorized("Topics.AuthenticationRequired", "Vui lòng đăng nhập để xem thống kê đề tài."));
        if (!currentUser.Roles.Any(role => role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
            return Result.Failure<TopicStatisticsDto>(Error.Forbidden("Topics.AccessDenied", "Bạn không có quyền xem thống kê đề tài."));
        if (batchId == Guid.Empty)
            return Result.Failure<TopicStatisticsDto>(Error.Validation("Topics.InvalidBatch", "Đợt khóa luận không hợp lệ."));
        return Result.Success(await reader.GetAsync(advisorId, batchId, cancellationToken));
    }
}
