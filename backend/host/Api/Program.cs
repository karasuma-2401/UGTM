using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using UGTM.Api.Modules;
using UGTM.Api.OpenApi;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;

var isBootstrapCommand = args.Length > 0
    && (string.Equals(args[0], "bootstrap-admin", StringComparison.OrdinalIgnoreCase)
        || args.Contains("--bootstrap-admin"));

var builder = WebApplication.CreateBuilder(isBootstrapCommand ? [] : args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration
        .AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    if (!isBootstrapCommand)
    {
        builder.Configuration.AddCommandLine(args);
    }
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

if (isBootstrapCommand)
{
    string? email = null;
    string? password = null;
    string? fullName = null;
    string? userCode = null;

    for (int i = 0; i < args.Length; i++)
    {
        if ((args[i] == "--email" || args[i] == "-e") && i + 1 < args.Length)
            email = args[++i];
        else if ((args[i] == "--fullname" || args[i] == "-n") && i + 1 < args.Length)
            fullName = args[++i];
        else if ((args[i] == "--usercode" || args[i] == "-c") && i + 1 < args.Length)
            userCode = args[++i];
    }

    email ??= app.Configuration["BOOTSTRAP_ADMIN_EMAIL"] ?? app.Configuration["UGTM_ADMIN_EMAIL"];
    password ??= app.Configuration["BOOTSTRAP_ADMIN_PASSWORD"] ?? app.Configuration["UGTM_ADMIN_PASSWORD"];
    fullName ??= app.Configuration["BOOTSTRAP_ADMIN_FULLNAME"] ?? app.Configuration["UGTM_ADMIN_FULLNAME"] ?? "System Administrator";
    userCode ??= app.Configuration["BOOTSTRAP_ADMIN_USERCODE"] ?? app.Configuration["UGTM_ADMIN_USERCODE"];

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        Console.Error.WriteLine("Error: Admin email and password are required. Supply the password through BOOTSTRAP_ADMIN_PASSWORD or UGTM_ADMIN_PASSWORD; command-line passwords are intentionally unsupported.");
        Console.Error.WriteLine("Usage: dotnet run --project backend/host/Api/UGTM.Api.csproj -- bootstrap-admin --email <email> [--fullname <name>] [--usercode <code>]");
        return 1;
    }

    using var scope = app.Services.CreateScope();
    var bootstrapper = scope.ServiceProvider.GetRequiredService<UGTM.Modules.Identity.Application.Bootstrap.IInitialAdminBootstrapper>();
    var result = await bootstrapper.BootstrapAdminAsync(email, password, fullName, userCode);

    if (result.IsSuccess)
    {
        Console.WriteLine($"[SUCCESS] Initial Admin user '{email}' created successfully with ID: {result.Value}");
        return 0;
    }
    else
    {
        Console.Error.WriteLine($"[FAILED] Initial Admin bootstrap failed: {result.Error.Code} - {result.Error.Message}");
        return 1;
    }
}

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
return 0;
