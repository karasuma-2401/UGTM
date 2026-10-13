using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace UGTM.Modules.Identity.Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.IsInRole("Admin")
            || context.User.HasClaim(ClaimTypes.Role, "Admin")
            || context.User.HasClaim("permission", requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}