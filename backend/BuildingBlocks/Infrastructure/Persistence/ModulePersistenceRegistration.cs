using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UGTM.BuildingBlocks.Infrastructure.Persistence;

public static class ModulePersistenceRegistration
{
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : ModuleDbContext
    {
        var connection = DatabaseOptions.ResolveConnectionString(configuration);
        return services.AddDbContext<TContext>(options => DatabaseOptions.Configure(options, connection));
    }
}
