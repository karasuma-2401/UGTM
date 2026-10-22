namespace UGTM.Modules.ThesisManagement.Infrastructure.Storage;

public static class S3StorageOptionsValidator
{
    public static void ValidateAndThrow(S3StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)
            || (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("S3Storage:Endpoint phải là URL HTTP hoặc HTTPS hợp lệ.");
        }

        if (!string.IsNullOrWhiteSpace(options.PublicEndpoint)
            && (!Uri.TryCreate(options.PublicEndpoint, UriKind.Absolute, out var publicEndpoint)
                || (!string.Equals(publicEndpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(publicEndpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException("S3Storage:PublicEndpoint phải là URL HTTP hoặc HTTPS hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey)
            || string.IsNullOrWhiteSpace(options.SecretKey)
            || string.IsNullOrWhiteSpace(options.BucketName)
            || string.IsNullOrWhiteSpace(options.Region))
        {
            throw new InvalidOperationException("S3Storage cần AccessKey, SecretKey, BucketName và Region hợp lệ.");
        }

        if (options.BucketName.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException("S3Storage:BucketName không được chứa khoảng trắng.");
        }
    }
}
