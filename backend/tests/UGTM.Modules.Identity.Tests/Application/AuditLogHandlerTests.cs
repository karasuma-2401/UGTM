using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.AuditLogs.Dtos;
using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;
using UGTM.Modules.Identity.Application.Common;

namespace UGTM.Modules.Identity.Tests.Application;

public sealed class AuditLogHandlerTests
{
    [Fact]
    public async Task HandleAsync_normalizes_pagination_and_filter_values()
    {
        var reader = new CapturingReader();
        var handler = new GetAuditLogsHandler(reader);

        var result = await handler.HandleAsync(
            new GetAuditLogsQuery(
                Action: "  Identity.User.Created  ",
                TargetType: "  User ",
                Outcome: " success ",
                PageNumber: 0,
                PageSize: 1000),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(reader.Query);
        Assert.Equal(1, reader.Query.PageNumber);
        Assert.Equal(100, reader.Query.PageSize);
        Assert.Equal("Identity.User.Created", reader.Query.Action);
        Assert.Equal("User", reader.Query.TargetType);
        Assert.Equal(AuditOutcomes.Success, reader.Query.Outcome);
    }

    [Fact]
    public async Task HandleAsync_uses_default_page_size_when_non_positive()
    {
        var reader = new CapturingReader();
        var handler = new GetAuditLogsHandler(reader);

        var result = await handler.HandleAsync(
            new GetAuditLogsQuery(PageSize: 0),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(20, reader.Query!.PageSize);
    }

    [Fact]
    public async Task HandleAsync_rejects_inverted_time_range()
    {
        var reader = new CapturingReader();
        var handler = new GetAuditLogsHandler(reader);
        var from = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(-1);

        var result = await handler.HandleAsync(
            new GetAuditLogsQuery(FromUtc: from, ToUtc: to),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.InvalidAuditLogFilter, result.Error);
        Assert.Null(reader.Query);
    }

    [Fact]
    public async Task HandleAsync_rejects_non_utc_time()
    {
        var reader = new CapturingReader();
        var handler = new GetAuditLogsHandler(reader);

        var result = await handler.HandleAsync(
            new GetAuditLogsQuery(
                FromUtc: new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Local)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.InvalidAuditLogFilter, result.Error);
        Assert.Null(reader.Query);
    }

    [Fact]
    public async Task HandleAsync_rejects_unknown_outcome()
    {
        var reader = new CapturingReader();
        var handler = new GetAuditLogsHandler(reader);

        var result = await handler.HandleAsync(
            new GetAuditLogsQuery(Outcome: "Unknown"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.InvalidAuditLogFilter, result.Error);
        Assert.Null(reader.Query);
    }

    private sealed class CapturingReader : IAuditLogQueryReader
    {
        public GetAuditLogsQuery? Query { get; private set; }

        public Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
            GetAuditLogsQuery query,
            CancellationToken cancellationToken)
        {
            Query = query;
            return Task.FromResult(new PagedResult<AuditLogDto>([], 0, query.PageNumber, query.PageSize));
        }
    }
}
