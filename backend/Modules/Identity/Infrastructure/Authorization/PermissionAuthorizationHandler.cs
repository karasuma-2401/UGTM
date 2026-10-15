using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private const string AdminRole = nameof(SystemRoleName.Admin);

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.IsInRole(AdminRole)
            || context.User.HasClaim(ClaimTypes.Role, AdminRole)
            || context.User.HasClaim("role", AdminRole)
            || context.User.HasClaim("permission", requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}