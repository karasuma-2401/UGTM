namespace UGTM.Modules.ThesisManagement.Infrastructure.Storage;

public sealed class S3StorageOptions
{
    public const string SectionName = "S3Storage";

    public string Endpoint { get; init; } = string.Empty;
    public string? PublicEndpoint { get; init; }
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string Region { get; init; } = "us-east-1";
}
