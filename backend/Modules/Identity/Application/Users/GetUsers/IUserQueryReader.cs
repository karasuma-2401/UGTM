using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.Users.Dtos;

namespace UGTM.Modules.Identity.Application.Users.GetUsers;

public interface IUserQueryReader
{
    Task<PagedResult<UserSummaryDto>> GetUsersAsync(
        GetUsersQuery query,
        CancellationToken cancellationToken);
}
