using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Infrastructure.Storage;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class S3ThesisSubmissionObjectStorageTests
{
    [Fact]
    public async Task CreateDownloadUriAsync_creates_a_short_lived_signed_get_url_without_making_a_network_call()
    {
        using var client = CreateClient();
        var storage = new S3ThesisSubmissionObjectStorage(client, Options.Create(CreateOptions()));

        var uri = await storage.CreateDownloadUriAsync(
            "thesis-submissions/syllabus/3c6bc3bd14bd4f808fced4b54ca4f93c/v1/a.pdf",
            TimeSpan.FromMinutes(5));

        Assert.Equal("localhost", uri.Host);
        Assert.Contains("X-Amz-", uri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateDownloadUriAsync_rejects_a_lifetime_longer_than_fifteen_minutes()
    {
        using var client = CreateClient();
        var storage = new S3ThesisSubmissionObjectStorage(client, Options.Create(CreateOptions()));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => storage.CreateDownloadUriAsync("thesis-submissions/syllabus/a.pdf", TimeSpan.FromMinutes(16)));
    }

    [Fact]
    public async Task CreateDownloadUriAsync_uses_the_public_endpoint_without_changing_the_internal_storage_endpoint()
    {
        using var client = CreateClient();
        using var storage = new S3ThesisSubmissionObjectStorage(
            client,
            Options.Create(new S3StorageOptions
            {
                Endpoint = "http://minio:9000",
                PublicEndpoint = "https://files.example.test",
                AccessKey = "test-access-key",
                SecretKey = "test-secret-key",
                BucketName = "thesis-submissions",
                Region = "us-east-1"
            }));

        var uri = await storage.CreateDownloadUriAsync("thesis-submissions/syllabus/a.pdf", TimeSpan.FromMinutes(5));

        Assert.Equal("files.example.test", uri.Host);
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
    }

    [Fact]
    public void Constructor_rejects_an_invalid_storage_endpoint()
    {
        using var client = CreateClient();
        var invalidOptions = new S3StorageOptions
        {
            Endpoint = "not-a-url",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
            BucketName = "thesis-submissions",
            Region = "us-east-1"
        };

        Assert.Throws<InvalidOperationException>(
            () => new S3ThesisSubmissionObjectStorage(client, Options.Create(invalidOptions)));
    }

    private static AmazonS3Client CreateClient()
        => new(
            new BasicAWSCredentials("test-access-key", "test-secret-key"),
            new AmazonS3Config
            {
                ServiceURL = "http://localhost:9000",
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                UseHttp = true
            });

    private static S3StorageOptions CreateOptions()
        => new()
        {
            Endpoint = "http://localhost:9000",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
            BucketName = "thesis-submissions",
            Region = "us-east-1"
        };
}
