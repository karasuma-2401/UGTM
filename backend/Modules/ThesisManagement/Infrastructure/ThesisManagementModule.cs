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
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.GetMidtermReportSubmissionHistory;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.ReviewMidtermReport;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.SubmitMidtermReport;
using UGTM.Modules.ThesisManagement.Application.ReviewGateSubmissions.GetReviewGateSubmissionFileDownload;
using UGTM.Modules.ThesisManagement.Application.ReviewGateDeadlines;
using UGTM.Modules.ThesisManagement.Infrastructure.ReviewGateDeadlines;
using UGTM.Modules.ThesisManagement.Application.ProgressMilestones.CreateProgressMilestone;
using UGTM.Modules.ThesisManagement.Application.ProgressMilestones.GetProgressMilestones;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.GetProgressReport;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.GetProgressReportFileDownload;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.ReviewProgressReport;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.SubmitProgressReport;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.AdvisorAssignTopicStudents;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.AdvisorUnassignTopicStudents;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.ConfirmTopicRegistration;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Expiration;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.GetMyRegistrationStatus;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.RegisterTopic;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.CreateRenameRequest;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.GetGroupRenameRequests;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.GetRenameRequests;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAcademicAffairs;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAdvisor;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByFaculty;
using UGTM.Modules.ThesisManagement.Infrastructure.BackgroundServices;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.SubmitFinalReport;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.GetFinalReportHistory;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.GetFinalReportFileDownload;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.BeginFinalReportEvaluation;
using UGTM.Modules.ThesisManagement.Contracts.AdvisorEvaluations;

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
        services.AddScoped<IMidtermReportRepository, MidtermReportRepository>();
        services.AddScoped<IFinalReportRepository, FinalReportRepository>();
        services.AddScoped<IAdvisorEvaluationWorkflowContract, AdvisorEvaluationWorkflowContract>();
        services.AddScoped<FinalReportSubmissionEligibilityChecker>();
        services.AddScoped<SubmitFinalReportHandler>();
        services.AddScoped<FinalReportReadAccessChecker>();
        services.AddScoped<GetFinalReportHistoryHandler>();
        services.AddScoped<GetFinalReportFileDownloadHandler>();
        services.AddScoped<BeginFinalReportEvaluationHandler>();
        services.AddScoped<IProgressMilestoneRepository, ProgressMilestoneRepository>();
        services.AddScoped<IProgressReportRepository, ProgressReportRepository>();
        services.AddScoped<ISubmissionFileRepository, SubmissionFileRepository>();
        services.AddScoped<ISubmissionReviewRepository, SubmissionReviewRepository>();
        services.AddScoped<IRenameRequestRepository, RenameRequestRepository>();
        services.AddScoped<IThesisUnitOfWork>(provider => provider.GetRequiredService<ThesisManagementDbContext>());
        services.AddScoped<IThesisRegistrationContract, ThesisRegistrationContract>();
        services.AddScoped<IThesisManagementAuditOutbox, ThesisManagementAuditOutbox>();
        services.AddScoped<IAuditIntegrationEventSource, ThesisManagementAuditEventSource>();
        services.Configure<S3StorageOptions>(configuration.GetSection(S3StorageOptions.SectionName));
        services.AddOptions<ReviewGateDeadlineEnforcementOptions>()
            .Bind(configuration.GetSection(ReviewGateDeadlineEnforcementOptions.SectionName))
            .Validate(
                options => options.PollingIntervalSeconds is >= 10 and <= 3600,
                "PollingIntervalSeconds must be between 10 and 3600 seconds.")
            .Validate(
                options => options.BatchSize is >= 1 and <= 1000,
                "BatchSize must be between 1 and 1000.")
            .ValidateOnStart();
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
        services.AddScoped<SubmitMidtermReportHandler>();
        services.AddScoped<ReviewMidtermReportHandler>();
        services.AddScoped<GetMidtermReportSubmissionHistoryHandler>();
        services.AddScoped<GetReviewGateSubmissionFileDownloadHandler>();
        services.AddScoped<CreateProgressMilestoneHandler>();
        services.AddScoped<GetProgressMilestonesHandler>();
        services.AddScoped<SubmitProgressReportHandler>();
        services.AddScoped<ReviewProgressReportHandler>();
        services.AddScoped<GetProgressReportHandler>();
        services.AddScoped<GetProgressReportFileDownloadHandler>();
        services.AddScoped<IReviewGateDeadlineEnforcer, ReviewGateDeadlineEnforcer>();
        services.AddHostedService<ReviewGateDeadlineEnforcementWorker>();

        // Topic Registrations
        services.AddScoped<ITopicRegistrationStore, TopicRegistrationStore>();
        services.AddScoped<RegisterTopicHandler>();
        services.AddScoped<ConfirmTopicRegistrationHandler>();
        services.AddScoped<GetMyRegistrationStatusHandler>();
        services.AddScoped<AdvisorAssignTopicStudentsHandler>();
        services.AddScoped<AdvisorUnassignTopicStudentsHandler>();
        services.AddScoped<ITopicRegistrationExpirationEnforcer, TopicRegistrationExpirationEnforcer>();
        services.AddHostedService<TopicRegistrationExpirationWorker>();

        // Rename Requests
        services.AddScoped<CreateRenameRequestHandler>();
        services.AddScoped<GetGroupRenameRequestsHandler>();
        services.AddScoped<GetRenameRequestsHandler>();
        services.AddScoped<ReviewRenameRequestByAdvisorHandler>();
        services.AddScoped<ReviewRenameRequestByFacultyHandler>();
        services.AddScoped<ReviewRenameRequestByAcademicAffairsHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReviewGateConfigurationEndpoints();
        endpoints.MapSyllabusSubmissionEndpoints();
        endpoints.MapMidtermReportSubmissionEndpoints();
        endpoints.MapFinalReportSubmissionEndpoints();
        endpoints.MapProgressMilestoneEndpoints();
        endpoints.MapProgressReportSubmissionEndpoints();
        endpoints.MapTopicRegistrationEndpoints();
        endpoints.MapRenameRequestEndpoints();
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
