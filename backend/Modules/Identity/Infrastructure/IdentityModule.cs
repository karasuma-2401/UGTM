using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UGTM.BuildingBlocks.Application;
using UGTM.Modules.Identity.Application.Activation;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;
using UGTM.Modules.Identity.Application.Authentication.ChangePassword;
using UGTM.Modules.Identity.Application.Authentication.GetCurrentUser;
using UGTM.Modules.Identity.Application.Authentication.Login;
using UGTM.Modules.Identity.Application.Authentication.Logout;
using UGTM.Modules.Identity.Application.Authentication.RefreshSession;
using UGTM.Modules.Identity.Application.Bootstrap;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Application.Permissions.GetPermissions;
using UGTM.Modules.Identity.Application.Provisioning;
using UGTM.Modules.Identity.Application.Recovery;
using UGTM.Modules.Identity.Application.Roles.GetRolePermissions;
using UGTM.Modules.Identity.Application.Roles.UpdateRolePermissions;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Application.Users.CreateUser;
using UGTM.Modules.Identity.Application.Users.GetUsers;
using UGTM.Modules.Identity.Application.Users.UpdateUser;
using UGTM.Modules.Identity.Application.Users.UpdateUserStatus;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Repositories;
using UGTM.Modules.Identity.Infrastructure.Authentication;
using UGTM.Modules.Identity.Infrastructure.Auditing;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.Identity.Infrastructure.Bootstrap;
using UGTM.Modules.Identity.Infrastructure.Endpoints;
using UGTM.Modules.Identity.Infrastructure.Notifications;
using UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;
using UGTM.Modules.Identity.Infrastructure.RateLimiting;

namespace UGTM.Modules.Identity.Infrastructure;

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityDbContext>(configuration);
        services.AddLogging();
        services.AddRouting();
        var dataProtection = services.AddDataProtection()
            .SetApplicationName("UGTM");
        var keyRingPath = configuration["Identity:DataProtection:KeyRingPath"];
        if (!string.IsNullOrWhiteSpace(keyRingPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        }
        services.AddHttpContextAccessor();
        services.AddIdentityRateLimits();
        var requireHttpsCookies = !string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"],
            "Development",
            StringComparison.OrdinalIgnoreCase);
        var cookiePolicy = new IdentityCookiePolicy(requireHttpsCookies);
        services.AddSingleton(cookiePolicy);
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = cookiePolicy.AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = requireHttpsCookies
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });

        services.AddOptions<IdentitySecurityPolicy>()
            .Bind(configuration.GetSection("Identity:Security"))
            .Validate(
                policy => policy.MaxFailedLoginAttempts > 0
                          && policy.ChallengeMaxAttempts > 0
                          && policy.LockoutDuration > TimeSpan.Zero
                          && policy.SessionIdleLifetime > TimeSpan.Zero
                          && policy.SessionAbsoluteLifetime > policy.SessionIdleLifetime
                          && policy.ActivationLifetime > TimeSpan.Zero
                          && policy.RecoveryOtpLifetime > TimeSpan.Zero
                          && policy.ResetGrantLifetime > TimeSpan.Zero,
                "Identity security lifetimes and attempt limits must be positive and consistent.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider
            .GetRequiredService<IOptions<IdentitySecurityPolicy>>().Value);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer)
                           && !string.IsNullOrWhiteSpace(options.Audience)
                           && !string.IsNullOrWhiteSpace(options.PrivateKeyPath)
                           && !string.IsNullOrWhiteSpace(options.KeyId)
                           && options.AccessTokenMinutes > 0,
                "JWT issuer, audience, private key path, key ID and lifetime are required.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider
            .GetRequiredService<IOptions<JwtOptions>>().Value);
        services.AddSingleton<JwtSigningKeyProvider>();
        services.AddOptions<IdentityEmailOptions>()
            .Bind(configuration.GetSection(IdentityEmailOptions.SectionName))
            .Validate(
                options => options.PublicWebBaseUrl is { IsAbsoluteUri: true }
                           && options.PublicWebBaseUrl.Scheme is "https" or "http",
                "Identity email public web base URL must be an absolute HTTP(S) URL.")
            .ValidateOnStart();

        services.AddScoped<IClock, SystemClock>();
        services.AddScoped<IPasswordService, AspNetPasswordService>();
        services.AddScoped<IPasswordPolicy, PasswordPolicy>();
        services.AddSingleton<IOpaqueTokenService, OpaqueTokenService>();
        services.AddScoped<IAccessTokenService, JwtAccessTokenService>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IAuthenticationSessionRepository, AuthenticationSessionRepository>();
        services.AddScoped<IOneTimeChallengeRepository, OneTimeChallengeRepository>();
        services.AddScoped<IUserProfileReader, UserProfileReader>();
        services.AddScoped<IUserQueryReader, UserQueryReader>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuditLogQueryReader, AuditLogQueryReader>();
        services.AddScoped<AuditIntegrationEventConsumer>();
        services.AddScoped<IIdentityEmailOutbox, IdentityEmailOutbox>();
        services.AddScoped<RefreshCookieService>();
        services.AddScoped<SessionValidator>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddScoped<ProvisioningStudentAccountHandler>();
        services.AddScoped<ValidateActivationHandler>();
        services.AddScoped<CompleteActivationHandler>();
        services.AddScoped<ResendActivationHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshSessionHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<StartPasswordRecoveryHandler>();
        services.AddScoped<VerifyRecoveryOtpHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<GetUsersHandler>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<UpdateUserStatusHandler>();
        services.AddScoped<GetPermissionsHandler>();
        services.AddScoped<GetRolePermissionsHandler>();
        services.AddScoped<UpdateRolePermissionsHandler>();
        services.AddScoped<GetAuditLogsHandler>();
        services.AddScoped<IInitialAdminBootstrapper, InitialAdminBootstrapper>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSigningKeyProvider, IOptions<JwtOptions>>((options, keys, configured) =>
            {
                var jwtOptions = configured.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = keys.Key,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub",
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context => context.HttpContext
                        .RequestServices
                        .GetRequiredService<SessionValidator>()
                        .ValidateAsync(context)
                };
            });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireAssertion(context =>
                    context.User.IsInRole("Admin")
                    || context.User.HasClaim(ClaimTypes.Role, "Admin")
                    || context.User.HasClaim("role", "Admin")));
        });
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddHostedService<IdentityOutboxProcessor>();
        services.AddHostedService<AuditIntegrationEventProcessor>();
        services.AddHostedService(sp => new InitialAdminBootstrapHostedService(
            sp,
            configuration,
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<InitialAdminBootstrapHostedService>>()));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapIdentityAuthEndpoints();
        endpoints.MapIdentityUserEndpoints();
        endpoints.MapIdentityRolePermissionEndpoints();
        endpoints.MapIdentityAuditLogEndpoints();

        endpoints.MapGroup($"/api/modules/{Name}")
            .WithTags(Name)
            .MapGet("/ping", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithSummary("Kiểm tra module Identity đang hoạt động")
            .WithDescription("""
                Không cần xác thực hoặc body. Trả 200 với module Identity và status ok khi endpoint có thể phục vụ request.
                Endpoint này không kiểm tra kết nối database, SMTP hoặc các dịch vụ phụ trợ.
                """)
            .WithName($"{Name}.Ping");
    }
}
