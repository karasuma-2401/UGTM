using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.Authentication.Login;
using UGTM.Modules.Identity.Infrastructure.Authentication;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class LoginEndpoint
{
    public sealed record Request(string Identifier, string Password);

    public static async Task<IResult> HandleAsync(
        Request request,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        LoginHandler handler,
        RefreshCookieService cookies,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(httpContext);

        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (userAgent.Length > 300)
        {
            userAgent = userAgent[..300];
        }

        var result = await handler.HandleAsync(
            new LoginCommand(
                request.Identifier,
                request.Password,
                string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
                null),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToHttpResult();
        }

        cookies.Write(
            httpContext.Response,
            result.Value.Tokens.RefreshToken,
            result.Value.Tokens.RefreshTokenExpiresAt);

        return Results.Ok(ApiResponse<object>.Ok(new
        {
            accessToken = result.Value.Tokens.AccessToken,
            accessTokenExpiresAt = result.Value.Tokens.AccessTokenExpiresAt,
            user = result.Value.User
        }));
    }
}
