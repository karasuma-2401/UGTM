using Microsoft.AspNetCore.Http;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public sealed class SensitiveResponseHeadersFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.HttpContext.Response.Headers.Pragma = "no-cache";
        context.HttpContext.Response.Headers.Expires = "0";
        return await next(context);
    }
}
