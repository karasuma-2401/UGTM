using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Application;
using UGTM.Modules.DefenseCouncil.Application.Grading;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Contracts.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.DefenseCouncil.Infrastructure.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.DecideAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.GetAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations;
using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;
using UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

namespace UGTM.Modules.DefenseCouncil.Infrastructure;

public sealed class DefenseCouncilModule : IModule
{
    public string Name => "DefenseCouncil";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<DefenseCouncilDbContext>(configuration);
        services.AddSingleton<ThesisGradeCalculator>();
        services.AddScoped<IGradeCalculationService, GradeCalculationService>();
        services.AddScoped<IAdvisorEvaluationRepository, AdvisorEvaluationRepository>();
        services.AddScoped<IAdvisorEvaluationIdempotencyRepository, AdvisorEvaluationIdempotencyRepository>();
        services.AddScoped<SaveAdvisorEvaluationHandler>();
        services.AddScoped<DecideAdvisorEvaluationHandler>();
        services.AddScoped<GetAdvisorEvaluationHandler>();
        services.AddScoped<IReviewerAssignmentEligibilityContract, ReviewerAssignmentEligibilityContract>();
        services.AddScoped<IDefenseCouncilUnitOfWork>(provider => provider.GetRequiredService<DefenseCouncilDbContext>());
        services.AddScoped<IDefenseCouncilAuditOutbox, DefenseCouncilAuditOutbox>();
        services.AddScoped<IAuditIntegrationEventSource, DefenseCouncilAuditEventSource>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAdvisorEvaluationEndpoints();
        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName($"{Name}.Ping");
    }
}
