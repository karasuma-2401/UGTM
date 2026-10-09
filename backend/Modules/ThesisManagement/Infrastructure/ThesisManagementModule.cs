using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Application;

namespace UGTM.Modules.ThesisManagement.Infrastructure;

public sealed class ThesisManagementModule : IModule
{
    public string Name => "ThesisManagement";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ThesisManagementDbContext>(configuration);
        services.AddScoped<IStudentGroupRepository, StudentGroupRepository>();
        services.AddScoped<IThesisUnitOfWork>(provider => provider.GetRequiredService<ThesisManagementDbContext>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName($"{Name}.Ping");
    }
}
