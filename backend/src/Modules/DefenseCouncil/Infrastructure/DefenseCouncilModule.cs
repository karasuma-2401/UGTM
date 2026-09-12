using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Application;

namespace UGTM.Modules.DefenseCouncil.Infrastructure;

public sealed class DefenseCouncilModule : IModule
{
    public string Name => "DefenseCouncil";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"/api/modules/{Name}/ping", () => Results.Ok(new { module = Name, status = "ok" }));
    }
}
