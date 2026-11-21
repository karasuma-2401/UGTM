using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;
using UGTM.Modules.Identity.Infrastructure.Authorization;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

public sealed class SessionValidator(
    IAuthenticationSessionRepository sessions,
    IClock clock)
{
    public async Task ValidateAsync(TokenValidatedContext context)
    {
        var subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Principal?.FindFirstValue("sub");
        var sessionClaim = context.Principal?.FindFirstValue("sid");

        if (!Guid.TryParse(subject, out var userId)
            || !Guid.TryParse(sessionClaim, out var sessionId))
        {
            context.Fail("Required identity claims are missing.");
            return;
        }

        var session = await sessions.GetSessionAsync(
            sessionId,
            context.HttpContext.RequestAborted);

        if (session is null
            || session.UserId != userId
            || session.User.Status != AccountStatus.Active
            || !session.CanRefresh(session.User.SecurityStamp, clock.UtcNow))
        {
            context.Fail("Session is no longer active.");
            return;
        }

        context.HttpContext.Items[PasswordChangeRequiredMiddleware.HttpContextItemKey] =
            session.User.MustChangePassword;
    }
}
