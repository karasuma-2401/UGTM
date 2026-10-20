using Microsoft.AspNetCore.Http;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Endpoints;

public static class EndpointResultMapper
{
    public static IResult ToHttpResult(this Result result)
        => result.IsSuccess
            ? Results.Ok(ApiResponse.Ok())
            : Results.Json(ApiResponse.Fail(ToApiError(result.Error)),
                statusCode: Status(result.Error.Type));

    public static IResult ToHttpResult<T>(this Result<T> result)
        => result.IsSuccess
            ? Results.Ok(ApiResponse<T>.Ok(result.Value))
            : Results.Json(ApiResponse<T>.Fail(ToApiError(result.Error)),
                statusCode: Status(result.Error.Type));

    private static ApiError ToApiError(Error error)
        => ApiError.Create(error.Code, error.Message);

    private static int Status(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError
    };
}
