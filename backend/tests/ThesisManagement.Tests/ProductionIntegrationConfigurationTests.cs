using Microsoft.Extensions.Configuration;
using UGTM.Api.Modules;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class ProductionIntegrationConfigurationTests
{
    [Fact]
    public void Production_integrations_accept_explicit_usable_configuration()
        => ProductionIntegrationConfiguration.Validate(Configuration());

    [Theory]
    [InlineData("ThesisManagement:Email:PublicWebBaseUrl", "http://localhost:5173")]
    [InlineData("ThesisManagement:Email:PublicWebBaseUrl", "https://localhost")]
    [InlineData("ThesisManagement:Email:OutboxPartition", "default")]
    [InlineData("ThesisManagement:Email:OutboxPartition", "")]
    [InlineData("PlagiarismAi:BaseUrl", "http://localhost:8000")]
    [InlineData("PlagiarismAi:BaseUrl", "not-a-url")]
    [InlineData("S3Storage:Endpoint", "")]
    [InlineData("S3Storage:AccessKey", "")]
    public void Production_integrations_reject_incomplete_or_local_defaults(string key, string value)
        => Assert.Throws<InvalidOperationException>(() => ProductionIntegrationConfiguration.Validate(Configuration(key, value)));

    [Fact]
    public void Production_rejects_outbox_partition_longer_than_the_supported_limit()
        => Assert.Throws<InvalidOperationException>(() => ProductionIntegrationConfiguration.Validate(
            Configuration("ThesisManagement:Email:OutboxPartition", new string('x', 101))));

    private static IConfiguration Configuration(string? key = null, string? value = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ThesisManagement:Email:PublicWebBaseUrl"] = "https://app.example.test",
            ["ThesisManagement:Email:OutboxPartition"] = "production",
            ["PlagiarismAi:BaseUrl"] = "http://ai:8000",
            ["S3Storage:Endpoint"] = "http://storage:9000",
            ["S3Storage:AccessKey"] = "access",
            ["S3Storage:SecretKey"] = "secret",
            ["S3Storage:BucketName"] = "bucket",
            ["S3Storage:Region"] = "us-east-1"
        };
        if (key is not null) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
