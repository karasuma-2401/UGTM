using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UGTM.BuildingBlocks.Application;

/// <summary>
/// Composition-root contract implemented once per module's Infrastructure project.
/// The Host discovers modules explicitly (an IModule[] literal in Program.cs) rather than
/// via assembly scanning, so a student team can step through wiring without reflection magic.
/// </summary>
public interface IModule
{
    string Name { get; }

    void RegisterModule(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
