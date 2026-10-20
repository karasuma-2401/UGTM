using System.Net;
using Microsoft.AspNetCore.Http;
using UGTM.Modules.Identity.Infrastructure.Authentication;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class ClientSessionMetadataProviderTests
{
    private const string FirstKey = "first-test-key-with-at-least-32-characters";
    private const string SecondKey = "second-test-key-with-at-least-32-characters";

    [Fact]
    public void Capture_normalizes_user_agent_and_pseudonymizes_ip()
    {
        var context = Context(
            IPAddress.Parse("203.0.113.10"),
            "  Mozilla/5.0\r\n   Chrome/140.0  ");
        var provider = Provider(FirstKey);

        var metadata = provider.Capture(context);

        Assert.Equal("Mozilla/5.0 Chrome/140.0", metadata.UserAgentSummary);
        Assert.NotNull(metadata.IpAddressHash);
        Assert.Equal(64, metadata.IpAddressHash.Length);
        Assert.DoesNotContain("203.0.113.10", metadata.IpAddressHash, StringComparison.Ordinal);
        Assert.Equal(metadata, provider.Capture(context));
    }

    [Fact]
    public void Capture_treats_ipv4_and_ipv4_mapped_address_as_same_client()
    {
        var provider = Provider(FirstKey);

        var ipv4 = provider.Capture(Context(IPAddress.Parse("192.0.2.15"), null));
        var mapped = provider.Capture(Context(IPAddress.Parse("::ffff:192.0.2.15"), null));

        Assert.Equal(ipv4.IpAddressHash, mapped.IpAddressHash);
    }

    [Fact]
    public void Capture_uses_configured_secret_as_hmac_key()
    {
        var context = Context(IPAddress.Parse("2001:db8::1"), null);

        var first = Provider(FirstKey).Capture(context);
        var second = Provider(SecondKey).Capture(context);

        Assert.NotEqual(first.IpAddressHash, second.IpAddressHash);
    }

    [Fact]
    public void Capture_limits_user_agent_and_handles_missing_ip()
    {
        var context = Context(null, new string('a', 500));

        var metadata = Provider(FirstKey, maxLength: 80).Capture(context);

        Assert.Equal(80, metadata.UserAgentSummary!.Length);
        Assert.Null(metadata.IpAddressHash);
    }

    private static ClientSessionMetadataProvider Provider(
        string key,
        int maxLength = 300)
        => new(new IdentityClientMetadataOptions
        {
            IpHashKey = key,
            UserAgentMaxLength = maxLength
        });

    private static DefaultHttpContext Context(
        IPAddress? remoteIpAddress,
        string? userAgent)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteIpAddress;
        if (userAgent is not null)
        {
            context.Request.Headers.UserAgent = userAgent;
        }

        return context;
    }
}
