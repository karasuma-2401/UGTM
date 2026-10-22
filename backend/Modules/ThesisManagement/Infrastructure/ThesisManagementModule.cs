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
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Infrastructure.Auditing;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Infrastructure.Storage;
using Amazon.S3;
using Microsoft.Extensions.Options;

using UGTM.Modules.ThesisManagement.Contracts.TopicRegistrations;

namespace UGTM.Modules.ThesisManagement.Infrastructure;

public sealed class ThesisManagementModule : IModule
{
    public string Name => "ThesisManagement";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ThesisManagementDbContext>(configuration);
        services.AddScoped<IStudentGroupRepository, StudentGroupRepository>();
        services.AddScoped<IThesisUnitOfWork>(provider => provider.GetRequiredService<ThesisManagementDbContext>());
        services.AddScoped<IThesisRegistrationContract, ThesisRegistrationContract>();
        services.AddScoped<IThesisManagementAuditOutbox, ThesisManagementAuditOutbox>();
        services.AddScoped<IAuditIntegrationEventSource, ThesisManagementAuditEventSource>();
        services.Configure<S3StorageOptions>(configuration.GetSection(S3StorageOptions.SectionName));
        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<S3StorageOptions>>().Value;
            S3StorageOptionsValidator.ValidateAndThrow(options);
            return S3ClientFactory.Create(options, options.Endpoint);
        });
        services.AddSingleton<IThesisSubmissionFileValidator, ThesisSubmissionFileValidator>();
        services.AddSingleton<IThesisSubmissionObjectKeyFactory, ThesisSubmissionObjectKeyFactory>();
        services.AddSingleton<IThesisSubmissionObjectStorage, S3ThesisSubmissionObjectStorage>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName($"{Name}.Ping");
    }
}
