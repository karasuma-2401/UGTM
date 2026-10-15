using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UGTM.Modules.Identity.Infrastructure.Authorization;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class AuthorizationTests
{
    [Fact]
    public void Http_current_user_reads_identity_claims()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim("sid", sessionId.ToString()),
            new Claim(ClaimTypes.Role, "Student"),
            new Claim("permission", "identity.users.read")
        ], "Bearer"));
        var context = new DefaultHttpContext { User = principal };
        var currentUser = new HttpCurrentUser(
            new HttpContextAccessor { HttpContext = context });

        Assert.True(currentUser.IsAuthenticated);
        Assert.Equal(userId, currentUser.UserId);
        Assert.Equal(sessionId, currentUser.SessionId);
        Assert.Contains("Student", currentUser.Roles);
        Assert.Contains("identity.users.read", currentUser.Permissions);
    }

    [Fact]
    public async Task Permission_handler_requires_an_exact_permission_claim()
    {
        var requirement = new PermissionRequirement("identity.users.read");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("permission", "identity.users.read")
        ], "Bearer"));
        var context = new AuthorizationHandlerContext(
            [requirement],
            principal,
            resource: null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Permission_policy_provider_builds_authenticated_dynamic_policy()
    {
        var provider = new PermissionPolicyProvider(
            Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync(
            PermissionPolicyProvider.Prefix + "identity.users.manage");

        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, x =>
            x is DenyAnonymousAuthorizationRequirement);
        var requirement = Assert.Single(
            policy.Requirements.OfType<PermissionRequirement>());
        Assert.Equal("identity.users.manage", requirement.Permission);
    }

    [Fact]
    public async Task Permission_handler_allows_admin_role_without_specific_permission()
    {
        var requirement = new PermissionRequirement("some.brand.new.future.permission");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "Admin")
        ], "Bearer"));
        var context = new AuthorizationHandlerContext(
            [requirement],
            principal,
            resource: null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Permission_handler_denies_non_admin_without_permission()
    {
        var requirement = new PermissionRequirement("identity.users.manage");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "Student")
        ], "Bearer"));
        var context = new AuthorizationHandlerContext(
            [requirement],
            principal,
            resource: null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
