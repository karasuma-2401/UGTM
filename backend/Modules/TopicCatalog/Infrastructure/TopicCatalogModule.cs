using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;
using UGTM.Modules.TopicCatalog.Application.Topics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetTopicTrackingDetail;
using UGTM.Modules.TopicCatalog.Infrastructure.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Application;

namespace UGTM.Modules.TopicCatalog.Infrastructure;

public sealed class TopicCatalogModule : IModule
{
    public string Name => "TopicCatalog";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TopicCatalogDbContext>(configuration);

        // Repositories & Query Readers
        services.AddScoped<ITopicRepository, TopicRepository>();
        services.AddScoped<ITopicQueryReader, TopicQueryReader>();

        // Query Handlers
        services.AddScoped<GetAllTopicsHandler>();
        services.AddScoped<GetMyTopicsHandler>();
        services.AddScoped<GetTopicTrackingDetailHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTopicEndpoints();

        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName($"{Name}.Ping");
    }
}
