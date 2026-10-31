using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Application;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.ApproveCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;
using UGTM.Modules.DefenseCouncil.Application.Councils.CreateCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Grading;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Auditing;
using UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;

namespace UGTM.Modules.DefenseCouncil.Infrastructure;

public sealed class DefenseCouncilModule : IModule
{
    public string Name => "DefenseCouncil";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<DefenseCouncilDbContext>(configuration);
        services.AddScoped<ICouncilRepository, CouncilRepository>();
        services.AddScoped<IDefenseCouncilUnitOfWork>(sp => sp.GetRequiredService<DefenseCouncilDbContext>());
        services.AddSingleton<ThesisGradeCalculator>();
        services.AddScoped<IGradeCalculationService, GradeCalculationService>();
        services.AddScoped<IDefenseCouncilAuditOutbox, DefenseCouncilAuditOutbox>();
        services.AddScoped<IAuditIntegrationEventSource, DefenseCouncilAuditEventSource>();

        // Council Use Cases
        services.AddScoped<CreateCouncilHandler>();
        services.AddScoped<AddCouncilMembersHandler>();
        services.AddScoped<AssignCouncilGroupsHandler>();
        services.AddScoped<ApproveCouncilHandler>();
        services.AddScoped<GetCouncilScheduleHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCouncilEndpoints();

        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName($"{Name}.Ping");
    }
}
