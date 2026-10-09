using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Application;
using UGTM.Modules.DefenseCouncil.Application.Grading;
using UGTM.Modules.DefenseCouncil.Contracts.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Grading;

namespace UGTM.Modules.DefenseCouncil.Infrastructure;

public sealed class DefenseCouncilModule : IModule
{
    public string Name => "DefenseCouncil";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<DefenseCouncilDbContext>(configuration);
        services.AddSingleton<ThesisGradeCalculator>();
        services.AddScoped<IGradeCalculationService, GradeCalculationService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName($"{Name}.Ping");
    }
}
