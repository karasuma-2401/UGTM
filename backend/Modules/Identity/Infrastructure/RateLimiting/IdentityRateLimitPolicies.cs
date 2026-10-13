using System.Threading.RateLimiting;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace UGTM.Modules.Identity.Infrastructure.RateLimiting;

public static class IdentityRateLimitPolicies
{
    public const string Login = "identity-login";
    public const string Activation = "identity-activation";
    public const string Recovery = "identity-recovery";

    public static IServiceCollection AddIdentityRateLimits(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = static async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(
                        MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds)
                            .ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    error = new
                    {
                        code = "Identity.RateLimitExceeded",
                        message = "Có quá nhiều yêu cầu. Vui lòng thử lại sau."
                    }
                }, ct);
            };
            AddFixed(options, Login, 10, TimeSpan.FromMinutes(1));
            AddFixed(options, Activation, 5, TimeSpan.FromMinutes(10));
            AddFixed(options, Recovery, 5, TimeSpan.FromMinutes(10));
        });
        return services;
    }

    private static void AddFixed(
        RateLimiterOptions options,
        string name,
        int permitLimit,
        TimeSpan window)
    {
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }
}
