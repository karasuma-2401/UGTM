using UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;
using UGTM.Modules.TopicCatalog.Tests.Fakes;

namespace UGTM.Modules.TopicCatalog.Tests.Application;

public sealed class GetMyTopicStatisticsHandlerTests
{
    [Fact]
    public async Task Mixed_role_statistics_are_scoped_to_the_signed_in_advisor_and_batch()
    {
        var user = FakeCurrentUser.AsAdmin();
        user.Roles = new HashSet<string> { "Admin", "Lecturer" };
        var reader = new Reader();
        var batch = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var result = await new GetMyTopicStatisticsHandler(reader, user).HandleAsync(batch, cancellation.Token);
        Assert.True(result.IsSuccess);
        Assert.Equal(new TopicStatisticsDto(84, 15, 7), result.Value);
        Assert.Equal((user.UserId!.Value, batch, cancellation.Token), reader.Last);
    }

    [Fact]
    public async Task Missing_identity_wrong_role_or_empty_batch_never_reads_statistics()
    {
        foreach (var user in new[] { FakeCurrentUser.Unauthenticated(), FakeCurrentUser.AsStudent(), FakeCurrentUser.AsFaculty() })
        {
            var reader = new Reader();
            Assert.False((await new GetMyTopicStatisticsHandler(reader, user).HandleAsync(null)).IsSuccess);
            Assert.Null(reader.Last);
        }
        var invalid = new Reader();
        Assert.False((await new GetMyTopicStatisticsHandler(invalid, FakeCurrentUser.AsLecturer()).HandleAsync(Guid.Empty)).IsSuccess);
        Assert.Null(invalid.Last);
    }

    private sealed class Reader : ITopicStatisticsReader
    {
        public (Guid, Guid?, CancellationToken)? Last;
        public Task<TopicStatisticsDto> GetAsync(Guid advisorId, Guid? batchId, CancellationToken cancellationToken)
        {
            Last = (advisorId, batchId, cancellationToken);
            return Task.FromResult(new TopicStatisticsDto(84, 15, 7));
        }
    }
}
