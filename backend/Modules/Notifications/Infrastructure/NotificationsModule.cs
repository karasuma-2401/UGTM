using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName($"{Name}.Ping")
            .ExcludeFromDescription();
    }
}
