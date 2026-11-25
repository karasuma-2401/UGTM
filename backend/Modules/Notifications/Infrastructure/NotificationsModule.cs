using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Application;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Application.Notifications.Abstractions;
using UGTM.Modules.Notifications.Application.Notifications.Handlers;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.Notifications.Infrastructure.Delivery;
using UGTM.Modules.Notifications.Infrastructure.Email;
using UGTM.Modules.Notifications.Infrastructure.Endpoints;
using UGTM.Modules.Notifications.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Infrastructure.Relay;
using UGTM.Modules.Notifications.Infrastructure.Security;

namespace UGTM.Modules.Notifications.Infrastructure;

public sealed class NotificationsModule : IModule
{
    public string Name => "Notifications";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NotificationsDbContext>(configuration);
        services.AddDataProtection();
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateOnStart();
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"]
                              ?? configuration["DOTNET_ENVIRONMENT"]
                              ?? Environments.Production;
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<SmtpOptions>>(
                new SmtpOptionsValidator(allowUnencrypted: string.Equals(
                    environmentName,
                    Environments.Development,
                    StringComparison.OrdinalIgnoreCase))));
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        var fallbackWebBaseUrl = configuration["Identity:Email:PublicWebBaseUrl"]
                                 ?? configuration["ThesisManagement:Email:PublicWebBaseUrl"]
                                 ?? "http://localhost:5173";
        services.AddOptions<NotificationDeliveryOptions>()
            .Configure(opts =>
            {
                opts.PublicWebBaseUrl = fallbackWebBaseUrl;
                opts.FcmEnabled = configuration.GetValue("Notifications:Fcm:Enabled", false);
            })
            .Bind(configuration.GetSection(NotificationDeliveryOptions.SectionName));

        services.AddSingleton<INotificationEmailTemplateRenderer, NotificationEmailTemplateRenderer>();
        services.AddScoped<INotificationDeliveryQueueStore, EfNotificationDeliveryQueueStore>();
        services.AddScoped<INotificationDeliveryDispatcher, NotificationDeliveryDispatcher>();
        services.AddHostedService<NotificationDeliveryWorker>();

        services.AddOptions<NotificationRelayOptions>()
            .Bind(configuration.GetSection(NotificationRelayOptions.SectionName));
        services.AddScoped<INotificationEventRelayConsumer, NotificationEventRelayConsumer>();
        services.AddHostedService<NotificationEventRelayWorker>();

        services.AddSingleton<IDeviceTokenProtector, DataProtectionDeviceTokenProtector>();
        services.AddScoped<INotificationStore, NotificationStore>();
        services.AddScoped<INotificationIngestionService, NotificationIngestionService>();

        services.AddScoped<GetMyNotificationsHandler>();
        services.AddScoped<GetUnreadNotificationCountHandler>();
        services.AddScoped<MarkNotificationReadHandler>();
        services.AddScoped<MarkAllNotificationsReadHandler>();
        services.AddScoped<RegisterDeviceHandler>();
        services.AddScoped<RevokeDeviceHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName($"{Name}.Ping")
            .ExcludeFromDescription();

        endpoints.MapNotificationEndpoints();
    }
}
