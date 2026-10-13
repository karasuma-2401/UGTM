using Microsoft.AspNetCore.Authorization;

namespace UGTM.Modules.Identity.Infrastructure.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
