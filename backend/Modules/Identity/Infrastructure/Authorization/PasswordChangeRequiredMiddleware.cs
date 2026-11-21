using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.Identity.Infrastructure.Authorization;

public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    public const string HttpContextItemKey = "UGTM.Identity.MustChangePassword";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint is null
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || endpoint.Metadata.GetMetadata<AllowWhenPasswordChangeRequiredMetadata>() is not null)
        {
            await next(context);
            return;
        }

        var mustChange = context.Items.TryGetValue(HttpContextItemKey, out var item) && item is bool liveFlag
            ? liveFlag
            : context.User.HasClaim(IdentityClaimTypes.PasswordChangeRequired, "true");

        if (!mustChange)
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        await context.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail(ApiError.Create(
                IdentityErrors.PasswordChangeRequired.Code,
                IdentityErrors.PasswordChangeRequired.Message)),
            context.RequestAborted);
    }
}

public static class PasswordChangeEnforcementApplicationBuilderExtensions
{
    public static IApplicationBuilder UseIdentityPasswordChangeEnforcement(this IApplicationBuilder app)
        => app.UseMiddleware<PasswordChangeRequiredMiddleware>();
}
