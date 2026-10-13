using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.Authentication.Logout;
using UGTM.Modules.Identity.Application.Authentication.GetCurrentUser;
using UGTM.Modules.Identity.Application.Authentication.RefreshSession;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Infrastructure.Authentication;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization();
        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous();
        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous();
        return group;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        GetCurrentUserHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(cancellationToken)).ToHttpResult();

    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        RefreshCookieService cookies,
        RefreshSessionHandler handler,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        var refreshToken = cookies.Read(context.Request);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result.Failure<object>(IdentityErrors.InvalidSession).ToHttpResult();
        }

        var result = await handler.HandleAsync(
            new RefreshSessionCommand(refreshToken),
            cancellationToken);
        if (result.IsFailure)
        {
            cookies.Delete(context.Response);
            return result.ToHttpResult();
        }

        cookies.Write(
            context.Response,
            result.Value.Tokens.RefreshToken,
            result.Value.Tokens.RefreshTokenExpiresAt);

        return Results.Ok(ApiResponse<object>.Ok(new
        {
            accessToken = result.Value.Tokens.AccessToken,
            accessTokenExpiresAt = result.Value.Tokens.AccessTokenExpiresAt,
            user = result.Value.User
        }));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        RefreshCookieService cookies,
        LogoutHandler handler,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        var result = await handler.HandleAsync(
            new LogoutCommand(cookies.Read(context.Request)),
            cancellationToken);
        cookies.Delete(context.Response);
        return result.ToHttpResult();
    }
}
