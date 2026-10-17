using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Application;
using UGTM.Modules.AcademicPeriods.Application.Auditing;
using UGTM.Modules.AcademicPeriods.Infrastructure.Auditing;
using UGTM.Modules.Identity.Contracts.Auditing;

namespace UGTM.Modules.AcademicPeriods.Infrastructure;

public sealed class AcademicPeriodsModule : IModule
{
    public string Name => "AcademicPeriods";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AcademicPeriodsDbContext>(configuration);
        services.AddScoped<IAcademicPeriodsAuditOutbox, AcademicPeriodsAuditOutbox>();
        services.AddScoped<IAuditIntegrationEventSource, AcademicPeriodsAuditEventSource>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName($"{Name}.Ping");
    }
}
