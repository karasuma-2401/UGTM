using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.AuditLogs.Dtos;
using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Contracts.Auditing;

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

    [Fact]
    public void NormalizeQuery_trims_search_and_exact_filters_and_rejects_excessive_input()
    {
        var result = GetAuditLogsHandler.NormalizeQuery(new GetAuditLogsQuery(
            SearchTerm: "  Nguyễn  ", RequestId: " req-1 ", TargetId: " 42 "));
        Assert.True(result.IsSuccess);
        Assert.Equal("Nguyễn", result.Value.SearchTerm);
        Assert.Equal("req-1", result.Value.RequestId);
        Assert.Equal("42", result.Value.TargetId);
        Assert.True(GetAuditLogsHandler.NormalizeQuery(new GetAuditLogsQuery(SearchTerm: new string('x', 201))).IsFailure);
    }

    [Fact]
    public void Integration_outcomes_accept_legacy_success_and_reject_unknown_values()
    {
        Assert.Equal("Success", AuditIntegrationEventValidation.NormalizeOutcome("Succeeded"));
        Assert.Equal("Denied", AuditIntegrationEventValidation.NormalizeOutcome("Denied"));
        Assert.Throws<ArgumentException>(() => AuditIntegrationEventValidation.NormalizeOutcome("Unknown"));
    }

    [Theory]
    [InlineData(null, null, "timestamp", "desc")]
    [InlineData(" Actor ", " ASC ", "actor", "asc")]
    [InlineData("Action", "Desc", "action", "desc")]
    [InlineData("OUTCOME", "asc", "outcome", "asc")]
    public async Task HandleAsync_normalizes_sort_parameters(
        string? sortBy,
        string? sortOrder,
        string expectedSortBy,
        string expectedSortOrder)
    {
        var reader = new CapturingReader();
        var handler = new GetAuditLogsHandler(reader);

        var result = await handler.HandleAsync(
            new GetAuditLogsQuery(SortBy: sortBy, SortOrder: sortOrder),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(reader.Query);
        Assert.Equal(expectedSortBy, reader.Query!.SortBy);
        Assert.Equal(expectedSortOrder, reader.Query!.SortOrder);
    }

    [Theory]
    [InlineData("targetType", "asc")]
    [InlineData("timestamp", "sideways")]
    public async Task HandleAsync_rejects_invalid_sort_parameters(string? sortBy, string? sortOrder)
    {
        var reader = new CapturingReader();
        var handler = new GetAuditLogsHandler(reader);

        var result = await handler.HandleAsync(
            new GetAuditLogsQuery(SortBy: sortBy, SortOrder: sortOrder),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidSort", result.Error.Code);
        Assert.Null(reader.Query);
    }

    private sealed class CapturingReader : IAuditLogQueryReader
    {
        public GetAuditLogsQuery? Query { get; private set; }

        public Task<AuditLogStatistics> GetStatisticsAsync(GetAuditLogsQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<List<string>> GetActionsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public IAsyncEnumerable<AuditLogDto> ExportAsync(GetAuditLogsQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
            GetAuditLogsQuery query,
            CancellationToken cancellationToken)
        {
            Query = query;
            return Task.FromResult(new PagedResult<AuditLogDto>([], 0, query.PageNumber, query.PageSize));
        }
    }
}
