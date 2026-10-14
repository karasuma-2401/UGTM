using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.Users.Dtos;

namespace UGTM.Modules.Identity.Application.Users.GetUsers;

public sealed class GetUsersHandler(IUserQueryReader queryReader)
{
    public async Task<Result<PagedResult<UserSummaryDto>>> HandleAsync(
        GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 10,
            > 100 => 100,
            _ => query.PageSize
        };

        var sanitizedQuery = query with { PageNumber = pageNumber, PageSize = pageSize };
        var result = await queryReader.GetUsersAsync(sanitizedQuery, cancellationToken);
        return Result.Success(result);
    }
}
