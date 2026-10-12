using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using UGTM.BuildingBlocks.Application.Responses;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class CsrfEndpoint
{
    public static IResult Handle(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(ApiResponse<object>.Ok(new
        {
            token = tokens.RequestToken
        }));
    }
}