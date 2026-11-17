using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using UGTM.Modules.ThesisManagement.Infrastructure.PlagiarismAI;
using Xunit;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class PlagiarismAiClientContractTests
{
    [Fact]
    public async Task ReadsSnakeCaseJobResponses()
    {
        var id = Guid.NewGuid();
        var client = CreateClient(request => request.Method == HttpMethod.Post
            ? Json(HttpStatusCode.Accepted, $$"""{"job_id":"{{id}}","status":"queued"}""")
            : Json(HttpStatusCode.OK, $$"""{"job_id":"{{id}}","type":"scan","status":"failed","error_code":"GPU_UNAVAILABLE_EXHAUSTED","attempt":5,"max_attempts":5,"retry_after":"2026-11-03T10:00:00Z"}"""));
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("file"));
        var submitted = await client.SubmitScanJobAsync("corpus", Guid.NewGuid(), 1, input, "thesis.pdf");
        var status = await client.GetJobStatusAsync(id);
        Assert.True(submitted.IsSuccess);
        Assert.Equal(id, submitted.JobId);
        Assert.Equal("GPU_UNAVAILABLE_EXHAUSTED", status!.ErrorCode);
        Assert.Equal(5, status.MaxAttempts);
        Assert.NotNull(status.RetryAfter);
    }

    [Fact]
    public async Task RejectsAcceptedResponseWithoutJobId()
    {
        var client = CreateClient(_ => Json(HttpStatusCode.Accepted, "{\"status\":\"queued\"}"));
        await using var input = new MemoryStream([1]);
        var submitted = await client.SubmitScanJobAsync("corpus", Guid.NewGuid(), 1, input, "thesis.pdf");
        Assert.False(submitted.IsSuccess);
    }

    [Fact]
    public async Task FetchesEveryReportPageBeforeReturning()
    {
        var requests = 0;
        var client = CreateClient(_ => Json(HttpStatusCode.OK, ++requests == 1
            ? "{\"summary\":{\"findings_returned\":1},\"findings\":[{\"submission_text\":\"first\"}],\"pagination\":{\"has_next\":true}}"
            : "{\"findings\":[{\"submission_text\":\"second\"}],\"pagination\":{\"has_next\":false}}"));
        var result = await client.GetReportRawJsonAsync(Guid.NewGuid());
        Assert.NotNull(result);
        Assert.Equal(2, requests);
        Assert.Equal(2, result.Value.GetProperty("findings").GetArrayLength());
        Assert.Equal(2, result.Value.GetProperty("summary").GetProperty("findings_returned").GetInt32());
    }

    private static PlagiarismAiClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new FakeHttpHandler(respond)) { BaseAddress = new Uri("http://localhost") },
            NullLogger<PlagiarismAiClient>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode code, string json) => new(code)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
