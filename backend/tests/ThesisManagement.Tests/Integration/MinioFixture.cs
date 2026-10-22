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

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MinioCollection : ICollectionFixture<MinioFixture>
{
    public const string Name = "ThesisManagement MinIO integration";
}
