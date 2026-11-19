using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Database.Migrations;

namespace UGTM.Api;

internal static class SchemaPreflight
{
    public static async Task<int> RunAsync(IConfiguration configuration)
    {
        try
        {
            var options = new DbContextOptionsBuilder<UGTMDbContext>();
            DatabaseOptions.Configure(options, DatabaseOptions.ResolveConnectionString(configuration),
                typeof(UGTMDbContext).Assembly.GetName().Name);
            await using var context = new UGTMDbContext(options.Options);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var expected = context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            var applied = (await context.Database.GetAppliedMigrationsAsync(deadline.Token)).ToHashSet(StringComparer.Ordinal);
            if (expected.Count == 0 || !expected.SetEquals(applied))
            {
                Console.Error.WriteLine("UGTM schema does not match this release. Review migrations before deployment.");
                Console.Error.WriteLine($"Pending migrations: {string.Join(", ", expected.Except(applied).Order())}");
                Console.Error.WriteLine($"Migrations outside this release: {string.Join(", ", applied.Except(expected).Order())}");
                return 1;
            }

            Console.WriteLine("UGTM schema preflight passed (read-only).");
            return 0;
        }
        catch (Exception exception)
        {
            // Driver exception messages may contain database credentials.
            Console.Error.WriteLine($"UGTM schema preflight failed ({exception.GetType().Name}); check connectivity and migration history.");
            return 1;
        }
    }
}
