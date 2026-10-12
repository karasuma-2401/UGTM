using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UGTM.BuildingBlocks.Application;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.Notifications.Infrastructure.Email;

namespace UGTM.Modules.Notifications.Infrastructure;

public sealed class NotificationsModule : IModule
{
    public string Name => "Notifications";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NotificationsDbContext>(configuration);
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host),
                "SMTP host is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.FromAddress),
                "SMTP from address is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.FromName),
                "SMTP from name is required.");
        services.AddScoped<IEmailSender, SmtpEmailSender>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName($"{Name}.Ping");
    }
}
