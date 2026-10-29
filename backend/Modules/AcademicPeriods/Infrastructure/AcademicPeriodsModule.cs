using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Application;
using UGTM.Modules.AcademicPeriods.Application.Auditing;
using UGTM.Modules.AcademicPeriods.Infrastructure.Auditing;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.AcademicPeriods.Application.Persistence;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;
using UGTM.Modules.AcademicPeriods.Application.Batches.ChangeKltnBatchStatus;
using UGTM.Modules.AcademicPeriods.Application.Batches.CreateKltnBatch;
using UGTM.Modules.AcademicPeriods.Application.Batches.Dtos;
using UGTM.Modules.AcademicPeriods.Application.Batches.Queries;
using UGTM.Modules.AcademicPeriods.Application.Batches.UpdateKltnBatchSchedule;
using UGTM.Modules.AcademicPeriods.Infrastructure.Endpoints;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.CreateDraft;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Publish;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Queries;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.UpdateDraft;

namespace UGTM.Modules.AcademicPeriods.Infrastructure;

public sealed class AcademicPeriodsModule : IModule
{
    public string Name => "AcademicPeriods";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AcademicPeriodsDbContext>(configuration);
        services.AddScoped<IKltnBatchReader, KltnBatchReader>();
        services.AddScoped<IAcademicBatchQueryReader, AcademicBatchQueryReader>();
        services.AddScoped<IScoreConfigurationQueryReader, ScoreConfigurationQueryReader>();
        services.AddScoped<IKltnBatchRepository, KltnBatchRepository>();
        services.AddScoped<IScheduleTemplateRepository, ScheduleTemplateRepository>();
        services.AddScoped<IScoreConfigurationRepository, ScoreConfigurationRepository>();
        services.AddScoped<IAcademicPeriodsUnitOfWork>(provider => provider.GetRequiredService<AcademicPeriodsDbContext>());
        services.AddScoped<IAcademicPeriodsAuditOutbox, AcademicPeriodsAuditOutbox>();
        services.AddScoped<IAuditIntegrationEventSource, AcademicPeriodsAuditEventSource>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<CreateKltnBatchHandler>();
        services.AddScoped<UpdateKltnBatchScheduleHandler>();
        services.AddScoped<OpenKltnBatchHandler>();
        services.AddScoped<CloseKltnBatchHandler>();
        services.AddScoped<ArchiveKltnBatchHandler>();
        services.AddScoped<GetKltnBatchesHandler>();
        services.AddScoped<GetKltnBatchByIdHandler>();
        services.AddScoped<GetKltnBatchTimelineHandler>();
        services.AddScoped<GetCurrentStageHandler>();
        services.AddScoped<CreateScoreConfigurationDraftHandler>();
        services.AddScoped<UpdateScoreConfigurationDraftHandler>();
        services.AddScoped<PublishScoreConfigurationHandler>();
        services.AddScoped<GetActiveScoreConfigurationHandler>();
        services.AddScoped<GetScoreConfigurationVersionHandler>();
        services.AddScoped<GetScoreConfigurationHistoryHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAcademicBatchEndpoints();
        endpoints.MapScoreConfigurationEndpoints();
        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName($"{Name}.Ping")
            .ExcludeFromDescription();
    }
}
