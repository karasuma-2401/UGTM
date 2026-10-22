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
using UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.GetReviewGateConfigurations;
using UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.UpsertReviewGateConfiguration;
using UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusSubmissionHistory;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusSubmissionFileDownload;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.ReviewSyllabus;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.SubmitSyllabus;

namespace UGTM.Modules.ThesisManagement.Infrastructure;

public sealed class ThesisManagementModule : IModule
{
    public string Name => "ThesisManagement";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ThesisManagementDbContext>(configuration);
        services.AddScoped<IStudentGroupRepository, StudentGroupRepository>();
        services.AddScoped<IReviewGateConfigurationRepository, ReviewGateConfigurationRepository>();
        services.AddScoped<ISyllabusRepository, SyllabusRepository>();
        services.AddScoped<ISubmissionFileRepository, SubmissionFileRepository>();
        services.AddScoped<ISubmissionReviewRepository, SubmissionReviewRepository>();
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
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<UpsertReviewGateConfigurationHandler>();
        services.AddScoped<GetReviewGateConfigurationsHandler>();
        services.AddScoped<SubmitSyllabusHandler>();
        services.AddScoped<ReviewSyllabusHandler>();
        services.AddScoped<GetSyllabusSubmissionHistoryHandler>();
        services.AddScoped<GetSyllabusSubmissionFileDownloadHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReviewGateConfigurationEndpoints();
        endpoints.MapSyllabusSubmissionEndpoints();
        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithSummary("Kiểm tra module Thesis Management đang hoạt động")
            .WithDescription("""
                Không cần xác thực hoặc body. Trả 200 với tên module và `status: ok` khi
                endpoint có thể phục vụ request. Endpoint này không kiểm tra database,
                object storage hoặc trạng thái deadline của các cổng thẩm định.
                """)
            .WithName($"{Name}.Ping");
    }
}
