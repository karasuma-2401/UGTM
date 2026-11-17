using Microsoft.Extensions.Configuration;
using UGTM.Modules.ThesisManagement.Infrastructure.Storage;

namespace UGTM.Api.Modules;

public static class ProductionIntegrationConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var publicUrl = configuration["ThesisManagement:Email:PublicWebBaseUrl"];
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var emailUrl)
            || emailUrl.Scheme != Uri.UriSchemeHttps || emailUrl.IsLoopback
            || !string.IsNullOrEmpty(emailUrl.UserInfo) || !string.IsNullOrEmpty(emailUrl.Fragment))
            throw new InvalidOperationException("Production ThesisManagement:Email:PublicWebBaseUrl must be an HTTPS, non-loopback URL.");

        var partition = configuration["ThesisManagement:Email:OutboxPartition"];
        if (string.IsNullOrWhiteSpace(partition) || partition.Trim().Length > 100
            || partition.Trim().Equals("default", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production ThesisManagement:Email:OutboxPartition must be explicitly configured and at most 100 characters.");

        var aiUrl = configuration["PlagiarismAi:BaseUrl"];
        if (!Uri.TryCreate(aiUrl, UriKind.Absolute, out var aiEndpoint)
            || (aiEndpoint.Scheme != Uri.UriSchemeHttp && aiEndpoint.Scheme != Uri.UriSchemeHttps)
            || aiEndpoint.IsLoopback || !string.IsNullOrEmpty(aiEndpoint.UserInfo)
            || !string.IsNullOrEmpty(aiEndpoint.Fragment))
            throw new InvalidOperationException("Production PlagiarismAi:BaseUrl must be a non-loopback HTTP or HTTPS URL.");

        var storage = configuration.GetSection(S3StorageOptions.SectionName).Get<S3StorageOptions>() ?? new S3StorageOptions();
        S3StorageOptionsValidator.ValidateAndThrow(storage);
    }
}
