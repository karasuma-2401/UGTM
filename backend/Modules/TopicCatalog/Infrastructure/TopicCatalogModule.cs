using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;
using UGTM.Modules.TopicCatalog.Application.Topics;
using UGTM.Modules.TopicCatalog.Application.Topics.CreateTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.UpdateTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.SubmitTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.ReviewTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.PublishTopics;
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
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.TopicCatalog.Infrastructure;

public sealed class TopicCatalogModule : IModule
{
    public string Name => "TopicCatalog";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TopicCatalogDbContext>(configuration);

        // Repositories, Unit of Work & Query Readers
        services.AddScoped<ITopicRepository, TopicRepository>();
        services.AddScoped<ITopicUnitOfWork>(provider => provider.GetRequiredService<TopicCatalogDbContext>());
        services.AddScoped<ITopicQueryReader, TopicQueryReader>();
        services.AddScoped<ITopicAdvisorAssignmentContract, TopicAdvisorAssignmentContract>();
        services.AddScoped<UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments.IReviewerAssignmentTopicReader,
            Persistence.Repositories.ReviewerAssignmentTopicReader>();
        services.AddScoped<ITopicSummaryReader, TopicSummaryReader>();
        services.AddScoped<ITopicSearchReader, TopicSearchReader>();
        services.AddScoped<ITopicRenameContract, TopicRenameContract>();

        // Command Handlers
        services.AddScoped<CreateTopicHandler>();
        services.AddScoped<UpdateTopicHandler>();
        services.AddScoped<SubmitTopicHandler>();
        services.AddScoped<ReviewTopicHandler>();
        services.AddScoped<PublishTopicsHandler>();

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
            .WithName($"{Name}.Ping")
            .ExcludeFromDescription();
    }
}
