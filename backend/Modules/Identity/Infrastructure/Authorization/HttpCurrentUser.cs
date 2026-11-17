using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.Identity.Infrastructure.Authorization;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal
    => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public Guid? UserId => Parse(ClaimTypes.NameIdentifier) ?? Parse("sub");
    public Guid? SessionId => Parse("sid");

    public IReadOnlySet<string> Roles => Principal.FindAll(ClaimTypes.Role)
        .Concat(Principal.FindAll("role"))
        .Select(x => x.Value)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> Permissions => Principal.FindAll("permission")
        .Select(x => x.Value).ToHashSet(StringComparer.Ordinal);

    private Guid? Parse(string claimType)
        => Guid.TryParse(Principal.FindFirstValue(claimType), out var value)
        ? value
        : null;
}