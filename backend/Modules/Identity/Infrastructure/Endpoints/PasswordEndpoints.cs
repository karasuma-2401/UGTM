using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.Modules.Identity.Application.Recovery;
using UGTM.Modules.Identity.Infrastructure.Authentication;

namespace UGTM.Modules.Identity.Infrastructure.Endpoints;

public static class PasswordEndpoints
{
    public sealed record StartRequest(string Email);
    public sealed record VerifyRequest(string ChallengeId, string Code);
    public sealed record ResetRequest(string ResetGrant, string NewPassword);

    public static RouteGroupBuilder MapPasswordEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/recovery/start", StartAsync);
        group.MapPost("/recovery/verify", VerifyAsync);
        group.MapPost("/recovery/reset", ResetAsync);
        return group;
    }

    private static async Task<IResult> StartAsync(
        StartRequest request,
        StartPasswordRecoveryHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new StartPasswordRecoveryCommand(request.Email),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> VerifyAsync(
        VerifyRequest request,
        VerifyRecoveryOtpHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new VerifyRecoveryOtpCommand(request.ChallengeId, request.Code),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> ResetAsync(
        ResetRequest request,
        HttpContext context,
        ResetPasswordHandler handler,
        RefreshCookieService cookies,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ResetPasswordCommand(request.ResetGrant, request.NewPassword),
            cancellationToken);

        if (result.IsSuccess)
        {
            cookies.Delete(context.Response);
        }

        return result.ToHttpResult();
    }
}
