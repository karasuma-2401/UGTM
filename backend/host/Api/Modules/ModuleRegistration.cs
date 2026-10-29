using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Application;
using UGTM.Modules.AcademicPeriods.Infrastructure;
using UGTM.Modules.DefenseCouncil.Infrastructure;
using UGTM.Modules.Identity.Infrastructure;
using UGTM.Modules.Notifications.Infrastructure;
using UGTM.Modules.ThesisManagement.Infrastructure;
using UGTM.Modules.TopicCatalog.Infrastructure;
using UGTM.Api.AdvisorEvaluations;
using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;

namespace UGTM.Api.Modules;

public static class ModuleRegistration
{
    public static IServiceCollection AddUgtmModules(this IServiceCollection services, IConfiguration configuration)
    {
        IModule[] modules =
        [
            new IdentityModule(),
            new AcademicPeriodsModule(),
            new TopicCatalogModule(),
            new ThesisManagementModule(),
            new DefenseCouncilModule(),
            new NotificationsModule(),
        ];

        foreach (var module in modules)
        {
            services.AddSingleton<IModule>(module);
            module.RegisterModule(services, configuration);
        }

        services.AddScoped<IAdvisorWorkflowTransactionCoordinator, AdvisorWorkflowTransactionCoordinator>();

        return services;
    }

    public static WebApplication MapUgtmModules(this WebApplication app)
    {
        foreach (var module in app.Services.GetServices<IModule>())
        {
            module.MapEndpoints(app);
        }

        return app;
    }
}
