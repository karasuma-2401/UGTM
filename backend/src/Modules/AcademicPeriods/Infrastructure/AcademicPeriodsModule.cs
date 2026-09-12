using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Application;

namespace UGTM.Modules.AcademicPeriods.Infrastructure;

public sealed class AcademicPeriodsModule : IModule
{
    public string Name => "AcademicPeriods";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"/api/modules/{Name}/ping", () => Results.Ok(new { module = Name, status = "ok" }));
    }
}
