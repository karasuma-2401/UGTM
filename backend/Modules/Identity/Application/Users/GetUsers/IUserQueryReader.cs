using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Application.Users.GetUsers;

public interface IUserQueryReader
{
    Task<PagedResult<UserSummaryDto>> GetUsersAsync(
        GetUsersQuery query,
        CancellationToken cancellationToken);

    Task<string?> ResolveRoleNameAsync(
        string roleName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return Task.FromResult<string?>(null);
        }

        var trimmed = roleName.Trim();
        if (long.TryParse(trimmed, out _)
            || !Enum.TryParse<SystemRoleName>(trimmed, ignoreCase: true, out var roleEnum)
            || !Enum.IsDefined(roleEnum))
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(roleEnum.ToString());
    }
}
