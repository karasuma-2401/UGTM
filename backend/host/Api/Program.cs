using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using UGTM.Api.Modules;
using UGTM.Api.OpenApi;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration
        .AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

builder.Services.AddUgtmOpenApi();

var knownProxyAddresses = builder.Configuration
    .GetSection("Proxy:KnownProxies")
    .Get<string[]>() ?? [];
var useForwardedHeaders = knownProxyAddresses.Length > 0;
if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        foreach (var address in knownProxyAddresses)
        {
            if (!IPAddress.TryParse(address, out var proxyAddress))
            {
                throw new InvalidOperationException(
                    $"Proxy:KnownProxies contains an invalid IP address: {address}");
            }

            options.KnownProxies.Add(proxyAddress);
        }
    });
}

const string FrontendCorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddUgtmModules(builder.Configuration);

var app = builder.Build();

if (useForwardedHeaders)
{
    app.UseForwardedHeaders();
}
app.UseMiddleware<GlobalExceptionMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("UGTM API")
        .AddPreferredSecuritySchemes("Bearer")
        .DisableAgent());

    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_WATCH")))
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                const string scalarUrl = "http://localhost:5287/scalar/v1";
                if (OperatingSystem.IsWindows())
                {
                    // Chạy qua explorer.exe trên Windows để tách rời tiến trình trình duyệt khỏi Process Tree / Job Object của Terminal
                    // Giúp trình duyệt không bị tắt khi người dùng tắt terminal hoặc kết thúc ứng dụng.
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{scalarUrl}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                else if (OperatingSystem.IsMacOS())
                {
                    System.Diagnostics.Process.Start("open", scalarUrl);
                }
                else if (OperatingSystem.IsLinux())
                {
                    System.Diagnostics.Process.Start("xdg-open", scalarUrl);
                }
            }
            catch
            {
                // Bỏ qua nếu môi trường headless hoặc container
            }
        });
    }
}

app.MapUgtmModules();

await app.RunAsync();
