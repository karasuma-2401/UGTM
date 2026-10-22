using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed class MinioFixture : IAsyncLifetime
{
    private const ushort ApiPort = 9000;

    private readonly IContainer _container = new ContainerBuilder("cgr.dev/chainguard/minio:latest")
        .WithPortBinding(ApiPort, true)
        .WithEnvironment("MINIO_ROOT_USER", "test-access-key")
        .WithEnvironment("MINIO_ROOT_PASSWORD", "test-secret-key")
        .WithCommand("server", "/data")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(ApiPort))
        .Build();

    public string Endpoint => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(ApiPort)}";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync(
                    $"{Endpoint}/minio/health/ready",
                    CancellationToken.None);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The TCP listener is available before the MinIO API is ready to handle S3 requests.
            }
            catch (TaskCanceledException)
            {
                // Retry the short per-request timeout until the fixture startup deadline is reached.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException("MinIO did not become ready within 20 seconds.");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MinioCollection : ICollectionFixture<MinioFixture>
{
    public const string Name = "ThesisManagement MinIO integration";
}
