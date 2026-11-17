using Amazon.Runtime;
using Amazon.S3;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Storage;

internal static class S3ClientFactory
{
    public static IAmazonS3 Create(S3StorageOptions options, string endpoint)
    {
        var endpointUri = new Uri(endpoint, UriKind.Absolute);

        return new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = options.Region,
                UseHttp = string.Equals(endpointUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            });
    }
}
