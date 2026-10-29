using Microsoft.AspNetCore.Http;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

internal static class EndpointResultMapper
{
    internal static IResult ToHttpResult<T>(this Result<T> result)
        => result.IsSuccess
            ? Results.Ok(ApiResponse<T>.Ok(result.Value))
            : Results.Json(
                ApiResponse<T>.Fail(ApiError.Create(result.Error.Code, result.Error.Message)),
                statusCode: result.Error.Type switch
                {
                    ErrorType.Validation => StatusCodes.Status400BadRequest,
                    ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                    ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                    ErrorType.NotFound => StatusCodes.Status404NotFound,
                    ErrorType.Conflict => StatusCodes.Status409Conflict,
                    ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
                    _ => StatusCodes.Status500InternalServerError
                });
}
