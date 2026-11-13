using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Application.StudentGroups.GetAdvisingGroups;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class AdvisingGroupQueryTests
{
    [Fact]
    public async Task Historical_groups_keep_their_identity_and_only_page_members_are_resolved()
    {
        var user = new User(["Admin", "Lecturer"]);
        var groupId = Guid.NewGuid();
        var student = Guid.NewGuid();
        var reader = new Reader { Page = new([
            new() { GroupId = groupId, TopicId = Guid.NewGuid(), BatchId = Guid.NewGuid(), TitleVi = "Nhóm đã dừng", Status = "Stopped", ConfirmedStudentsCount = 1 }
        ], 42, new(42, 30, 10, 2, 31, 3, 1), [new(groupId, student, "Confirmed", DateTime.UtcNow, DateTime.UtcNow, null)]) };
        var students = new Students();
        var query = new GetAdvisingGroupsQuery(Scope: "stopped", SortBy: "title", SortOrder: "asc", PageNumber: 2, PageSize: 20);
        using var cancellation = new CancellationTokenSource();
        var result = await new GetAdvisingGroupsHandler(reader, students, user).HandleAsync(query, cancellation.Token);
        Assert.True(result.IsSuccess);
        Assert.Equal(user.UserId, reader.AdvisorId);
        Assert.Equal(query, reader.Query);
        Assert.Equal(cancellation.Token, reader.CancellationToken);
        Assert.Equal(new[] { student }, students.Requested);
        Assert.Equal(42, result.Value.Page.TotalCount);
        Assert.Equal(2, result.Value.Page.PageNumber);
        Assert.Equal("Stopped", result.Value.Page.Items.Single().Status);
        Assert.Equal(groupId, result.Value.Page.Items.Single().GroupId);
        Assert.Null(result.Value.Page.Items.Single().Students.Single().FullName);
        Assert.Equal(2, result.Value.Statistics.Stopped);
    }

    [Fact]
    public async Task Authentication_and_role_failures_never_query_groups_or_identities()
    {
        foreach (var user in new[] { new User(["Lecturer"], false), new User(["Student"]), new User(["Faculty"]), new User(["CouncilMember"]) })
        {
            var reader = new Reader(); var students = new Students();
            Assert.False((await new GetAdvisingGroupsHandler(reader, students, user).HandleAsync(new())).IsSuccess);
            Assert.Null(reader.Query);
            Assert.Empty(students.Requested);
        }
    }

    [Fact]
    public async Task Invalid_filters_cannot_fall_back_to_a_broader_scope()
    {
        var queries = new[] {
            new GetAdvisingGroupsQuery(BatchId: Guid.Empty), new(Scope: "invalid"), new(Status: "999"),
            new(Status: "NotAGroupStatus"), new(Confirmation: "Confirmed"), new(SortBy: "sql"), new(SortOrder: "invalid"), new(Keyword: new string('x',201))
        };
        foreach (var query in queries)
        {
            var reader = new Reader();
            Assert.False((await new GetAdvisingGroupsHandler(reader, new Students(), new User(["Lecturer"])).HandleAsync(query)).IsSuccess);
            Assert.Null(reader.Query);
        }
    }

    [Fact]
    public async Task Paging_is_bounded_and_cancellation_is_forwarded()
    {
        var reader = new Reader();
        var result = await new GetAdvisingGroupsHandler(reader, new Students(), new User(["Lecturer"]))
            .HandleAsync(new(Keyword: "  Nguyễn Đức  ", PageNumber: -4, PageSize: 1000));
        Assert.True(result.IsSuccess);
        Assert.Equal("Nguyễn Đức", reader.Query!.Keyword);
        Assert.Equal(1, reader.Query.PageNumber);
        Assert.Equal(100, reader.Query.PageSize);
    }

    [Theory]
    [InlineData("Nguyễn ĐỨC", "nguyen duc")]
    [InlineData("Đề tài %_\\", "de tai %_\\")]
    public void Search_normalizes_vietnamese_without_turning_literal_wildcards_into_filters(string input, string expected)
        => Assert.Equal(expected, GetAdvisingGroupsHandler.NormalizeSearch(input));

    private sealed class Reader : IAdvisingGroupQueryReader
    {
        public Guid? AdvisorId;
        public GetAdvisingGroupsQuery? Query;
        public CancellationToken CancellationToken;
        public AdvisingGroupPage Page = new([], 0, new(0,0,0,0,0,0,0), []);
        public Task<AdvisingGroupPage> GetAsync(Guid advisorId, GetAdvisingGroupsQuery query, CancellationToken cancellationToken)
        { AdvisorId = advisorId; Query = query; CancellationToken = cancellationToken; return Task.FromResult(Page); }
    }
    private sealed class Students : IStudentInfoReader
    {
        public Guid[] Requested = [];
        public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        { Requested = ids.ToArray(); return Task.FromResult<IReadOnlyDictionary<Guid, StudentInfoContractDto>>(new Dictionary<Guid, StudentInfoContractDto>()); }
        public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(Guid? id, string? code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class User(string[] roles, bool authenticated = true) : ICurrentUser
    {
        public bool IsAuthenticated => authenticated;
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles => roles.ToHashSet();
        public IReadOnlySet<string> Permissions => new HashSet<string>();
    }
}
