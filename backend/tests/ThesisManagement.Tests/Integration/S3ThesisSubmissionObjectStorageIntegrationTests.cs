using System.Net;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Infrastructure.Storage;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

[Collection(MinioCollection.Name)]
public sealed class S3ThesisSubmissionObjectStorageIntegrationTests(MinioFixture fixture)
{
    [Fact]
    public async Task Upload_presigned_download_and_delete_keep_submission_objects_private()
    {
        var bucketName = $"thesis-smoke-{Guid.NewGuid():N}";
        const string objectKey = "thesis-submissions/syllabus/3c6bc3bd14bd4f808fced4b54ca4f93c/v1/a.pdf";
        var content = Encoding.ASCII.GetBytes("%PDF-1.7\nprivate thesis submission");

        using var client = CreateClient();
        var storage = new S3ThesisSubmissionObjectStorage(
            client,
            Options.Create(new S3StorageOptions
            {
                Endpoint = fixture.Endpoint,
                AccessKey = "test-access-key",
                SecretKey = "test-secret-key",
                BucketName = bucketName,
                Region = "us-east-1"
            }));

        await client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });

        try
        {
            await storage.UploadAsync(
                new ThesisSubmissionObjectWriteRequest(objectKey, "application/pdf", content.Length),
                new MemoryStream(content));

            using var httpClient = new HttpClient();
            using var anonymousResponse = await httpClient.GetAsync($"{fixture.Endpoint}/{bucketName}/{objectKey}");
            Assert.Equal(HttpStatusCode.Forbidden, anonymousResponse.StatusCode);

            var signedUri = await storage.CreateDownloadUriAsync(objectKey, TimeSpan.FromMinutes(5));
            Assert.Equal(Uri.UriSchemeHttp, signedUri.Scheme);
            var downloadedContent = await httpClient.GetByteArrayAsync(signedUri);
            Assert.Equal(content, downloadedContent);

            await storage.DeleteAsync(objectKey);

            await Assert.ThrowsAsync<AmazonS3Exception>(
                () => client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                }));
        }
        finally
        {
            await TryDeleteObjectAsync(client, bucketName, objectKey);
            await TryDeleteBucketAsync(client, bucketName);
        }
    }

    private AmazonS3Client CreateClient()
        => new(
            new BasicAWSCredentials("test-access-key", "test-secret-key"),
            new AmazonS3Config
            {
                ServiceURL = fixture.Endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                UseHttp = true
            });

    private static async Task TryDeleteObjectAsync(IAmazonS3 client, string bucketName, string objectKey)
    {
        try
        {
            await client.DeleteObjectAsync(bucketName, objectKey);
        }
        catch (AmazonS3Exception)
        {
            // The bucket may not have been created if the test setup failed.
        }
    }

    private static async Task TryDeleteBucketAsync(IAmazonS3 client, string bucketName)
    {
        try
        {
            await client.DeleteBucketAsync(bucketName);
        }
        catch (AmazonS3Exception)
        {
            // Keep cleanup best-effort so the original test failure is preserved.
        }
    }
}
