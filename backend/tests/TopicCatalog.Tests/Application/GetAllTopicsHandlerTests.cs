using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;
using UGTM.Modules.TopicCatalog.Tests.Fakes;

namespace UGTM.Modules.TopicCatalog.Tests.Application;

public sealed class GetAllTopicsHandlerTests
{
    [Fact]
    public async Task GetAllTopicsHandler_success_with_valid_parameters()
    {
        var stubReader = new StubTopicQueryReader
        {
            GetAllTopicsResponse = new PagedResult<TopicSummaryDto>(
                Items: [
                    new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Đề tài A", "Topic A", "Mô tả", "Yêu cầu", "Approved", 2, DateTime.UtcNow)
                ],
                TotalCount: 1,
                PageNumber: 1,
                PageSize: 10)
        };
        var handler = new GetAllTopicsHandler(stubReader);

        var result = await handler.HandleAsync(
            new GetAllTopicsQuery("AI", null, 1, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Đề tài A", result.Value.Items[0].TitleVi);
        Assert.NotNull(stubReader.LastGetAllQuery);
        Assert.Equal("AI", stubReader.LastGetAllQuery.Keyword);
        Assert.Equal(1, stubReader.LastGetAllQuery.PageNumber);
        Assert.Equal(10, stubReader.LastGetAllQuery.PageSize);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(-999, 1)]
    public async Task GetAllTopicsHandler_sanitizes_invalid_page_number_to_one(int rawPage, int expectedPage)
    {
        var stubReader = new StubTopicQueryReader();
        var handler = new GetAllTopicsHandler(stubReader);

        var result = await handler.HandleAsync(
            new GetAllTopicsQuery(null, null, rawPage, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stubReader.LastGetAllQuery);
        Assert.Equal(expectedPage, stubReader.LastGetAllQuery.PageNumber);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-10, 10)]
    [InlineData(101, 100)]
    [InlineData(500, 100)]
    [InlineData(99999, 100)]
    public async Task GetAllTopicsHandler_clamps_page_size_boundaries(int rawPageSize, int expectedPageSize)
    {
        var stubReader = new StubTopicQueryReader();
        var handler = new GetAllTopicsHandler(stubReader);

        var result = await handler.HandleAsync(
            new GetAllTopicsQuery(null, null, 1, rawPageSize),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stubReader.LastGetAllQuery);
        Assert.Equal(expectedPageSize, stubReader.LastGetAllQuery.PageSize);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    [InlineData("'; DROP TABLE topics; --")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("🤖 Học sâu & Xử lý ngôn ngữ tự nhiên 🚀")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetAllTopicsHandler_handles_special_characters_and_edge_case_keywords_safely(string? keyword)
    {
        var stubReader = new StubTopicQueryReader();
        var handler = new GetAllTopicsHandler(stubReader);

        var result = await handler.HandleAsync(
            new GetAllTopicsQuery(keyword, null, 1, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stubReader.LastGetAllQuery);
        Assert.Equal(keyword, stubReader.LastGetAllQuery.Keyword);
    }

    [Fact]
    public async Task GetAllTopicsHandler_returns_empty_result_when_no_topics_exist()
    {
        var stubReader = new StubTopicQueryReader
        {
            GetAllTopicsResponse = new PagedResult<TopicSummaryDto>([], 0, 1, 10)
        };
        var handler = new GetAllTopicsHandler(stubReader);

        var result = await handler.HandleAsync(
            new GetAllTopicsQuery("NonExistentTopic999", Guid.NewGuid(), 1, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }
}
