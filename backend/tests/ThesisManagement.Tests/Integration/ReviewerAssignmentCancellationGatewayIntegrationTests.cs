using System.Net;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Fact]
    public async Task Cancellation_http_preserves_the_delete_json_body_and_receipt_through_the_production_caddy_route()
    {
        var original = await CreateForCancellationAsync();
        var client = await HttpAsync();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docker", "Caddyfile"))) root = root.Parent;
        Assert.NotNull(root);
        var caddyfile = await File.ReadAllTextAsync(Path.Combine(root.FullName, "docker", "Caddyfile"));
        Assert.Contains("@backend path /api/*", caddyfile);
        Assert.Contains("reverse_proxy backend:8080", caddyfile);
        var compose = await File.ReadAllLinesAsync(Path.Combine(root.FullName, "docker", "docker-compose.prod.yml"));
        var image = compose.Single(value => value.Trim().StartsWith("image: caddy:", StringComparison.Ordinal))
            .Trim()["image: ".Length..];
        var port = checked((ushort)client.BaseAddress!.Port);
        // Keep the production routing/header/encoding rules. Only replace the upstream address and use HTTP on a test port.
        caddyfile = caddyfile.Replace("backend:8080", $"host.testcontainers.internal:{port}", StringComparison.Ordinal);
        await TestcontainersSettings.ExposeHostPortsAsync(port);
        await using var gateway = new ContainerBuilder(image)
            .WithPortBinding(8080, true)
            .WithEnvironment("API_HOST", "http://:8080")
            .WithEnvironment("ACME_EMAIL", "gateway@test.example")
            .WithResourceMapping(Encoding.UTF8.GetBytes(caddyfile), "/etc/caddy/Caddyfile")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(probe =>
                probe.ForPort(8080).ForPath(CreationRoute).ForStatusCode(HttpStatusCode.MethodNotAllowed),
                wait => wait.WithTimeout(TimeSpan.FromSeconds(45))))
            .Build();
        await gateway.StartAsync();
        using var proxied = new HttpClient(new SocketsHttpHandler { UseProxy = false })
        { BaseAddress = new Uri($"http://{gateway.Hostname}:{gateway.GetMappedPublicPort(8080)}") };
        var key = Guid.NewGuid();
        const string reason = "Điều chỉnh GVPB qua gateway.\nLý do nội bộ: đổi lịch phản biện.";
        using var request = CancelRequest(original.Id, new { expectedAssignmentVersion = original.Version, reason }, key);
        using var response = await SendThroughGatewayAsync();
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, payload);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        using var json = JsonDocument.Parse(payload);
        Assert.Equal(reason, json.RootElement.GetProperty("data").GetProperty("cancellationReason").GetString());
        using var replay = await DeleteAsync(original.Id, original.Version, reason, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(payload, await replay.Content.ReadAsStringAsync());
        await AssertCountsAsync(1, 2, 2);

        async Task<HttpResponseMessage> SendThroughGatewayAsync()
        {
            try { return await proxied.SendAsync(request); }
            catch (HttpRequestException exception)
            {
                var (stdout, stderr) = await gateway.GetLogsAsync();
                throw new InvalidOperationException($"Caddy transport failed. Stdout: {stdout}\nStderr: {stderr}", exception);
            }
        }
    }
}
