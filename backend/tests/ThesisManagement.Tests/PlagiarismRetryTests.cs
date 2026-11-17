using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using Xunit;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class PlagiarismRetryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RetryChangesLocalStateOnlyAfterAiAccepts(bool accepted)
    {
        var jobId = Guid.NewGuid();
        var scan = FinalReportPlagiarismScan.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow);
        scan.MarkQueued(jobId, DateTime.UtcNow);
        scan.MarkCompleted(.2, .8, 1, 10, 2, 0, 1, 0, "{}", DateTime.UtcNow);
        var ai = new FakeAi(accepted);
        var uow = new FakeUnitOfWork();
        var handler = new ExecutePlagiarismScanHandler(ai, null!, uow, Options.Create(new PlagiarismAiOptions()),
            NullLogger<ExecutePlagiarismScanHandler>.Instance);

        var result = await handler.RetryAsync(scan);

        Assert.Equal(accepted, result);
        Assert.Equal(jobId, ai.RequestedJob);
        Assert.Equal(jobId, scan.JobId);
        Assert.Equal(accepted ? PlagiarismScanStatus.Queued : PlagiarismScanStatus.Completed, scan.Status);
        Assert.Equal(accepted ? null : .2, scan.SimilarityCoverage);
        Assert.Equal(accepted ? 1 : 0, uow.Saves);
    }

    private sealed class FakeUnitOfWork : IThesisUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { Saves++; return Task.FromResult(1); }
    }

    private sealed class FakeAi(bool accepted) : IPlagiarismAiClient
    {
        public Guid? RequestedJob { get; private set; }
        public Task<bool> RetryJobAsync(Guid jobId, CancellationToken ct = default) { RequestedJob = jobId; return Task.FromResult(accepted); }
        public Task<ScanJobSubmissionResult> SubmitScanJobAsync(string corpusId, Guid submissionId, int version, Stream fileStream, string fileName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PlagiarismJobStatusDto?> GetJobStatusAsync(Guid jobId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<JsonElement?> GetReportRawJsonAsync(Guid jobId, int schema = 3, int page = 1, int pageSize = 100, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ScanJobSubmissionResult> IndexCorpusDocumentAsync(string corpusId, string sourceId, int version, Stream fileStream, string fileName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> CheckHealthAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }
}
