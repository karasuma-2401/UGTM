using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Application.Users.GetUsers;

public sealed class GetUsersHandler(IUserQueryReader queryReader)
{
    public async Task<Result<PagedResult<UserSummaryDto>>> HandleAsync(
        GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var trimmedRole = query.Role.Trim();
            if (int.TryParse(trimmedRole, out _)
                || !Enum.TryParse<SystemRoleName>(trimmedRole, ignoreCase: true, out var roleEnum)
                || !Enum.IsDefined(roleEnum))
            {
                return Result.Failure<PagedResult<UserSummaryDto>>(IdentityErrors.InvalidRole);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var trimmedStatus = query.Status.Trim();
            if (int.TryParse(trimmedStatus, out _)
                || !Enum.TryParse<AccountStatus>(trimmedStatus, ignoreCase: true, out var statusEnum)
                || !Enum.IsDefined(statusEnum))
            {
                return Result.Failure<PagedResult<UserSummaryDto>>(IdentityErrors.InvalidStatus);
            }
        }

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
