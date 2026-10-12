using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.Modules.Identity.Application.Activation;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class ActivationEndpoints
{
    public sealed record ValidateRequest(string Token);
    public sealed record CompleteRequest(string Token, string Password);
    public sealed record ResendRequest(string Identifier);

    public static RouteGroupBuilder MapActivationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/activation/validate", ValidateAsync);
        group.MapPost("/activation/complete", CompleteAsync);
        group.MapPost("/activation/resend", ResendAsync);
        return group;
    }

    private static async Task<IResult> ValidateAsync(
        ValidateRequest request,
        ValidateActivationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new ValidateActivationCommand(request.Token),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> CompleteAsync(
        CompleteRequest request,
        CompleteActivationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new CompleteActivationCommand(request.Token, request.Password),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> ResendAsync(
        ResendRequest request,
        ResendActivationHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new ResendActivationCommand(request.Identifier),
            cancellationToken)).ToHttpResult();
}
