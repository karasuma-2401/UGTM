using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace UGTM.Modules.Identity.Infrastructure.Authorization;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "Permission:";

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string name)
    {
        if (!name.StartsWith(Prefix, StringComparison.Ordinal))
            return base.GetPolicyAsync(name);

        var permission = name[Prefix.Length..];
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}