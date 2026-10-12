using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class AuthEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapIdentityAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity/auth")
            .WithTags("Identity.Auth");

        group.MapGet("/csrf", CsrfEndpoint.Handle);
        group.MapPost("/login", LoginEndpoint.HandleAsync);
        group.MapActivationEndpoints();
        group.MapSessionEndpoints();
        group.MapPasswordEndpoints();

        return endpoints;
    }
}
