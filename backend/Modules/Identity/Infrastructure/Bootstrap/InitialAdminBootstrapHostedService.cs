using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UGTM.Modules.Identity.Application.Bootstrap;

namespace UGTM.Modules.Identity.Infrastructure.Bootstrap;

public sealed class InitialAdminBootstrapHostedService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<InitialAdminBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = configuration["BOOTSTRAP_ADMIN_EMAIL"]
            ?? configuration["UGTM_ADMIN_EMAIL"]
            ?? configuration["Identity:BootstrapAdmin:Email"];

        var password = configuration["BOOTSTRAP_ADMIN_PASSWORD"]
            ?? configuration["UGTM_ADMIN_PASSWORD"]
            ?? configuration["Identity:BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogDebug("Không tìm thấy biến môi trường khởi tạo Admin (BOOTSTRAP_ADMIN_EMAIL, BOOTSTRAP_ADMIN_PASSWORD). Bỏ qua tự động bootstrap.");
            return;
        }

        var fullName = configuration["BOOTSTRAP_ADMIN_FULLNAME"]
            ?? configuration["UGTM_ADMIN_FULLNAME"]
            ?? configuration["Identity:BootstrapAdmin:FullName"]
            ?? "System Administrator";

        var userCode = configuration["BOOTSTRAP_ADMIN_USERCODE"]
            ?? configuration["UGTM_ADMIN_USERCODE"]
            ?? configuration["Identity:BootstrapAdmin:UserCode"];

        try
        {
            using var scope = serviceProvider.CreateScope();
            var bootstrapper = scope.ServiceProvider.GetRequiredService<IInitialAdminBootstrapper>();

            var result = await bootstrapper.BootstrapAdminAsync(
                email,
                password,
                fullName,
                userCode,
                cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation("Đã tự động khởi tạo tài khoản Quản trị viên (Admin) đầu tiên qua biến môi trường thành công: {Email}", email);
            }
            else if (result.Error.Code == "Identity.AdminAlreadyExists")
            {
                logger.LogInformation("Tự động khởi tạo Admin qua biến môi trường bỏ qua: Hệ thống đã có tài khoản Admin.");
            }
            else
            {
                logger.LogWarning("Tự động khởi tạo Admin thất bại: {Code} - {Message}", result.Error.Code, result.Error.Message);
            }
        }
        finally
        {
            configuration["BOOTSTRAP_ADMIN_PASSWORD"] = null;
            configuration["UGTM_ADMIN_PASSWORD"] = null;
            configuration["Identity:BootstrapAdmin:Password"] = null;
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", null);
            Environment.SetEnvironmentVariable("UGTM_ADMIN_PASSWORD", null);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
