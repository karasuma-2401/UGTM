using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UGTM.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddUgtmPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_CONNECTION_STRING"]
            ?? "Host=localhost;Database=ugtm;Username=postgres;Password=postgres";

        services.AddDbContext<UGTMDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", Schemas.Shared))
                .UseSnakeCaseNamingConvention());

        return services;
    }
}
