using Microsoft.AspNetCore.Http;
using UGTM.Modules.Identity.Infrastructure.Endpoints;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class SensitiveResponseHeadersFilterTests
{
    [Fact]
    public async Task Filter_disables_client_and_proxy_caching()
    {
        var httpContext = new DefaultHttpContext();
        var invocation = new TestInvocationContext(httpContext);
        var filter = new SensitiveResponseHeadersFilter();

        var result = await filter.InvokeAsync(invocation, _ => ValueTask.FromResult<object?>("ok"));

        Assert.Equal("ok", result);
        Assert.Equal("no-store", httpContext.Response.Headers.CacheControl);
        Assert.Equal("no-cache", httpContext.Response.Headers.Pragma);
        Assert.Equal("0", httpContext.Response.Headers.Expires);
    }

    private sealed class TestInvocationContext(HttpContext httpContext)
        : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = httpContext;
        public override IList<object?> Arguments { get; } = [];

        public override T GetArgument<T>(int index)
            => (T)Arguments[index]!;
    }
}
