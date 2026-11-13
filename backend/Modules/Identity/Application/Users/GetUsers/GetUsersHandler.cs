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
        string? accountGroup = null;
        if (!string.IsNullOrWhiteSpace(query.AccountGroup))
        {
            var input = query.AccountGroup.Trim();
            if (input.Contains(',') || int.TryParse(input, out _)
                || !Enum.TryParse<UserAccountGroup>(input, ignoreCase: true, out var parsedGroup)
                || !Enum.IsDefined(parsedGroup))
            {
                return Result.Failure<PagedResult<UserSummaryDto>>(IdentityErrors.InvalidAccountGroup);
            }
            accountGroup = parsedGroup.ToString();
        }

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

        var sortBy = NormalizeSortBy(query.SortBy);
        if (sortBy is null)
        {
            return Result.Failure<PagedResult<UserSummaryDto>>(IdentityErrors.InvalidSort);
        }

        var sortOrder = NormalizeSortOrder(query.SortOrder);
        if (sortOrder is null)
        {
            return Result.Failure<PagedResult<UserSummaryDto>>(IdentityErrors.InvalidSort);
        }

        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize switch
        {
            < 1 => 10,
            > 100 => 100,
            _ => query.PageSize
        };

        var sanitizedQuery = query with
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            AccountGroup = accountGroup,
            SortBy = sortBy,
            SortOrder = sortOrder
        };
        var result = await queryReader.GetUsersAsync(sanitizedQuery, cancellationToken);
        return Result.Success(result);
    }

    private static string? NormalizeSortBy(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return "createdat";
        }

        return sortBy.Trim().ToLowerInvariant() switch
        {
            "fullname" => "fullname",
            "usercode" => "usercode",
            "lastloginat" => "lastloginat",
            "status" => "status",
            "createdat" => "createdat",
            _ => null
        };
    }

    private static string? NormalizeSortOrder(string? sortOrder)
    {
        if (string.IsNullOrWhiteSpace(sortOrder))
        {
            return "desc";
        }

        return sortOrder.Trim().ToLowerInvariant() switch
        {
            "asc" => "asc",
            "desc" => "desc",
            _ => null
        };
    }
}
