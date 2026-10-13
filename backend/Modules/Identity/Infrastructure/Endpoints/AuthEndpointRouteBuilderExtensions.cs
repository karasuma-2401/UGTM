using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.Modules.Identity.Infrastructure.RateLimiting;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class AuthEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapIdentityAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity/auth")
            .WithTags("Identity.Auth");

        group.MapGet("/csrf", CsrfEndpoint.Handle)
            .AllowAnonymous();
        group.MapPost("/login", LoginEndpoint.HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityRateLimitPolicies.Login);
        group.MapActivationEndpoints();
        group.MapSessionEndpoints();
        group.MapPasswordEndpoints();

        return endpoints;
    }
}
