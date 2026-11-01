using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UGTM.Modules.ThesisManagement.Application.Storage;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Storage;

public sealed class S3ThesisSubmissionObjectStorage : IThesisSubmissionObjectStorage, IDisposable
{
    private static readonly TimeSpan MaximumDownloadUriLifetime = TimeSpan.FromMinutes(15);

    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonS3 _presigningClient;
    private readonly S3StorageOptions _options;
    private readonly string _presigningEndpoint;
    private readonly bool _ownsPresigningClient;

    public S3ThesisSubmissionObjectStorage(IAmazonS3 s3Client, IOptions<S3StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(s3Client);
        ArgumentNullException.ThrowIfNull(options);

        _s3Client = s3Client;
        _options = options.Value;
        S3StorageOptionsValidator.ValidateAndThrow(_options);

        _presigningEndpoint = string.IsNullOrWhiteSpace(_options.PublicEndpoint)
            ? _options.Endpoint
            : _options.PublicEndpoint;

        _ownsPresigningClient = !string.Equals(
            _options.Endpoint.TrimEnd('/'),
            _presigningEndpoint.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
        _presigningClient = _ownsPresigningClient
            ? S3ClientFactory.Create(_options, _presigningEndpoint)
            : _s3Client;
    }

    public async Task UploadAsync(
        ThesisSubmissionObjectWriteRequest request,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (content is null || !content.CanRead)
        {
            throw new ArgumentException("The upload content must be readable.", nameof(content));
        }

        ValidateObjectKey(request.ObjectKey);

        if (string.IsNullOrWhiteSpace(request.ContentType) || request.FileSize <= 0)
        {
            throw new ArgumentException("The upload metadata is invalid.", nameof(request));
        }

        var putObjectRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = request.ObjectKey,
            ContentType = request.ContentType,
            InputStream = content,
            AutoCloseStream = false,
            AutoResetStreamPosition = false
        };

        try
        {
            await _s3Client.PutObjectAsync(putObjectRequest, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (string.Equals(ex.ErrorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase))
        {
            await _s3Client.PutBucketAsync(_options.BucketName, cancellationToken);
            if (content.CanSeek)
            {
                content.Position = 0;
            }
            await _s3Client.PutObjectAsync(putObjectRequest, cancellationToken);
        }
    }

    public Task<Uri> CreateDownloadUriAsync(
        string objectKey,
        TimeSpan validFor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateObjectKey(objectKey);

        if (validFor <= TimeSpan.Zero || validFor > MaximumDownloadUriLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(validFor),
                $"The download URL lifetime must be between 1 second and {MaximumDownloadUriLifetime.TotalMinutes:0} minutes.");
        }

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Protocol = string.Equals(new Uri(_presigningEndpoint, UriKind.Absolute).Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                ? Protocol.HTTP
                : Protocol.HTTPS,
            Expires = DateTime.UtcNow.Add(validFor)
        };

        return Task.FromResult(new Uri(_presigningClient.GetPreSignedURL(request)));
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        ValidateObjectKey(objectKey);

        await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_ownsPresigningClient)
        {
            _presigningClient.Dispose();
        }
    }

    private static void ValidateObjectKey(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey)
            || objectKey.StartsWith("/", StringComparison.Ordinal)
            || objectKey.Contains('\\', StringComparison.Ordinal)
            || objectKey.Split('/', StringSplitOptions.None).Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new ArgumentException("The object key is invalid.", nameof(objectKey));
        }
    }
}
