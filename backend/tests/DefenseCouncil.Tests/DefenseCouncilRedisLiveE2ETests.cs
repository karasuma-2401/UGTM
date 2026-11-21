using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Infrastructure.Caching;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Common;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.ExportAcademicResults;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResultDetail;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResults;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.ApproveCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;
using UGTM.Modules.DefenseCouncil.Application.Councils.CheckCouncilDiscrepancy;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.CreateCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMinuteWorkspace;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilsOverview;
using UGTM.Modules.DefenseCouncil.Application.Councils.LockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilScore;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilToAcademicAffairs;
using UGTM.Modules.DefenseCouncil.Application.Councils.UnlockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Grading;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Contracts.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;
using UGTM.Modules.DefenseCouncil.Infrastructure.Exporting;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Tests;

[CollectionDefinition(Name)]
public sealed class RedisCacheCollection : ICollectionFixture<RedisCacheFixture>
{
    public const string Name = "DefenseCouncil isolated Redis";
}

public sealed class RedisCacheFixture : IAsyncLifetime
{
    private const string Password = "redis-integration-test-only";
    private readonly IContainer _container = new ContainerBuilder("redis:7-alpine")
        .WithCommand("redis-server", "--requirepass", Password)
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("redis-cli", "-a", Password, "ping"))
        .Build();

    public string ConnectionString => $"{_container.Hostname}:{_container.GetMappedPublicPort(6379)},password={Password},abortConnect=false,connectTimeout=2000,syncTimeout=2000";
    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[Collection(RedisCacheCollection.Name)]
public sealed class DefenseCouncilRedisLiveE2ETests(RedisCacheFixture redis) : IAsyncLifetime
{
    private readonly string LiveRedisInstancePrefix = $"ugtm_live_e2e:{Guid.NewGuid():N}:";

    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private IConnectionMultiplexer? _redisMultiplexer;
    private IDatabase? _redisDb;

    // Fakes and seeds
    private readonly FakeCouncilRepository _councilRepo = new();
    private readonly FakeDefenseCouncilUnitOfWork _uow = new();
    private readonly FakeBatchReader _batchReader = new();
    private readonly FakeCouncilUserInfoReader _userInfoReader = new();
    private readonly FakeStudentInfoReader _studentInfoReader = new();
    private readonly FakeStudentSearchReader _studentSearchReader = new();
    private readonly FakeDefenseGroupContract _defenseGroupContract = new();
    private readonly FakeTopicDefenseContract _topicDefenseContract = new();
    private readonly FakeDefenseCouncilAuditOutbox _auditOutbox = new();
    private readonly FakeReauthenticationVerifier _reauthVerifier = new();
    private readonly FakeAcademicResultsThesisReader _thesisReader = new();
    private readonly FakeScoreSummaryStore _scoreStore = new();
    private readonly FakeArchiveProofContract _archiveProofContract = new();

    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _topicId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _student1Id = Guid.NewGuid();
    private readonly Guid _student2Id = Guid.NewGuid();
    private readonly Guid _chairmanId = Guid.NewGuid();
    private readonly Guid _secretaryId = Guid.NewGuid();
    private readonly Guid _memberId = Guid.NewGuid();
    private readonly Guid _advisorId = Guid.NewGuid();
    private readonly Guid _reviewerId = Guid.NewGuid();
    private readonly Guid _facultyId = Guid.NewGuid();
    private readonly Guid _academicAffairsId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        // A disposable Redis is required. Infrastructure failures must not become passing tests.
        _redisMultiplexer = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        _redisDb = _redisMultiplexer.GetDatabase();
        await _redisDb.PingAsync();

        // 2. Seed domain entities & contract data
        SeedData();

        // 3. Build TestServer with the isolated Redis Distributed Cache.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = redis.ConnectionString,
            ["Redis:InstanceName"] = LiveRedisInstancePrefix
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // Register UGTM Caching (Points directly to LIVE Redis)
        builder.Services.AddUgtmCaching(configuration);

        // Register dependencies
        builder.Services.AddSingleton<ThesisGradeCalculator>();
        builder.Services.AddScoped<IGradeCalculationService, GradeCalculationService>();
        builder.Services.AddSingleton<ICouncilRepository>(_councilRepo);
        builder.Services.AddSingleton<IDefenseCouncilUnitOfWork>(_uow);
        builder.Services.AddSingleton<IKltnBatchReader>(_batchReader);
        builder.Services.AddSingleton<ICouncilUserInfoReader>(_userInfoReader);
        builder.Services.AddSingleton<IStudentInfoReader>(_studentInfoReader);
        builder.Services.AddSingleton<IStudentSearchReader>(_studentSearchReader);
        builder.Services.AddSingleton<IDefenseGroupContract>(_defenseGroupContract);
        builder.Services.AddSingleton<ITopicDefenseContract>(_topicDefenseContract);
        builder.Services.AddSingleton<IDefenseCouncilAuditOutbox>(_auditOutbox);
        builder.Services.AddSingleton<IReauthenticationVerifier>(_reauthVerifier);
        builder.Services.AddSingleton<IAcademicResultsThesisReader>(_thesisReader);
        builder.Services.AddSingleton<IScoreSummaryStore>(_scoreStore);
        builder.Services.AddSingleton<IFinalSubmissionArchiveProofContract>(_archiveProofContract);

        // Register Council Handlers
        builder.Services.AddScoped<CouncilMinuteWorkspaceLoader>();
        builder.Services.AddSingleton<UGTM.Modules.DefenseCouncil.Application.ScoreSummaries.ICouncilMinuteWriteGuard, TestCouncilScoreWriteGuard>();
        builder.Services.AddScoped<IFinalSubmissionAuthorityReader, TestScoreAuthority>();
        builder.Services.AddSingleton<IScoreSummaryConfigurationReader, FakeMinuteConfigurationReader>();
        builder.Services.AddSingleton<ICouncilSchedulingGuard, FakeCouncilSchedulingGuard>();

        builder.Services.AddScoped<CreateCouncilHandler>();
        builder.Services.AddScoped<AddCouncilMembersHandler>();
        builder.Services.AddScoped<AssignCouncilGroupsHandler>();
        builder.Services.AddScoped<ApproveCouncilHandler>();
        builder.Services.AddScoped<GetCouncilScheduleHandler>();
        builder.Services.AddScoped<GetCouncilMinuteWorkspaceHandler>();
        builder.Services.AddScoped<SaveCouncilMinuteHandler>();
        builder.Services.AddScoped<LockCouncilMinuteHandler>();
        builder.Services.AddScoped<UnlockCouncilMinuteHandler>();
        builder.Services.AddScoped<GetCouncilsOverviewHandler>();
        builder.Services.AddScoped<UpdateCouncilScheduleHandler>();
        builder.Services.AddScoped<SubmitCouncilToAcademicAffairsHandler>();
        builder.Services.AddScoped<CheckCouncilDiscrepancyHandler>();
        builder.Services.AddScoped<SubmitCouncilScoreHandler>();

        // Register Academic Results Handlers
        builder.Services.AddScoped<AcademicResultsWorkspaceLoader>();
        builder.Services.AddScoped<IAcademicResultsExportFileGenerator, AcademicResultsExportFileGenerator>();
        builder.Services.AddScoped<GetAcademicResultsHandler>();
        builder.Services.AddScoped<GetAcademicResultDetailHandler>();
        builder.Services.AddScoped<ExportAcademicResultsHandler>();

        // Authentication & Authorization
        builder.Services.AddAuthentication(TestLiveE2EAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestLiveE2EAuthHandler>(TestLiveE2EAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();

        _app.MapCouncilEndpoints();
        _app.MapAcademicAffairsResultEndpoints();

        await _app.StartAsync();
        _client = _app.GetTestClient();
        _client.BaseAddress = new Uri("http://localhost");
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null)
        {
            await _app.DisposeAsync();
        }

        if (_redisMultiplexer != null)
        {
            await _redisMultiplexer.DisposeAsync();
        }
    }

    private void Authenticate(Guid userId, string role)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", $"test.{userId:N}.{role}");
    }

    private void SeedData()
    {
        _batchReader.ExistingBatches.Add(_batchId);

        // Users
        _userInfoReader.Users[_chairmanId] = new CouncilUserInfoContractDto(_chairmanId, "PGS. TS. Trần Chủ Tịch", "ct@uit.edu.vn", "CB001", true);
        _userInfoReader.Users[_secretaryId] = new CouncilUserInfoContractDto(_secretaryId, "ThS. Hoàng Thư Ký", "tk@uit.edu.vn", "CB002", true);
        _userInfoReader.Users[_memberId] = new CouncilUserInfoContractDto(_memberId, "TS. Lê Ủy Viên", "uv@uit.edu.vn", "CB003", true);
        _userInfoReader.Users[_advisorId] = new CouncilUserInfoContractDto(_advisorId, "TS. Đặng Hướng Dẫn", "hd@uit.edu.vn", "CB004", true);
        _userInfoReader.Users[_reviewerId] = new CouncilUserInfoContractDto(_reviewerId, "TS. Phan Phản Biện", "pb@uit.edu.vn", "CB005", true);
        _userInfoReader.Users[_facultyId] = new CouncilUserInfoContractDto(_facultyId, "Giáo Vụ Khoa", "gvk@uit.edu.vn", "CB006", true);
        _userInfoReader.Users[_academicAffairsId] = new CouncilUserInfoContractDto(_academicAffairsId, "Cán Bộ PĐT", "pdt@uit.edu.vn", "CB008", true);

        _userInfoReader.Users[_student1Id] = new CouncilUserInfoContractDto(_student1Id, "Nguyễn Văn An", "an@uit.edu.vn", "22520001", true);
        _userInfoReader.Users[_student2Id] = new CouncilUserInfoContractDto(_student2Id, "Trần Thị Bình", "binh@uit.edu.vn", "22520002", true);

        // Topics & Groups
        _topicDefenseContract.Topics[_topicId] = new TopicDefenseDetailsContractDto(_topicId, _batchId, _advisorId, "Hệ thống AI nhận diện giọng nói", "AI Voice Recognition System");
        _defenseGroupContract.Groups[_groupId] = new DefenseGroupSnapshotContractDto(
            _groupId, _topicId, _batchId, "InProgress", DateTime.UtcNow,
            [
                new(_student1Id, _batchId, "Confirmed"),
                new(_student2Id, _batchId, "Confirmed")
            ]);
        _councilRepo.ActiveReviewers[_groupId] = _reviewerId;

        // Academic results thesis reader
        var g1Members = new List<DefenseGroupMemberContractDto>
        {
            new(_student1Id, _batchId, "Confirmed"),
            new(_student2Id, _batchId, "Confirmed")
        };
        var g1Midterm = new AcademicResultMidtermSnapshotContractDto(Guid.NewGuid(), "Approved", 1, "Báo cáo tốt", _advisorId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-30));
        _thesisReader.Groups.Add(new AcademicResultGroupSnapshotContractDto(_groupId, _topicId, _batchId, "InProgress", DateTime.UtcNow.AddDays(-60), g1Members, g1Midterm, "ArchiveConfirmed"));
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").Clone();
    }

    #region Live Redis E2E Tests

    [Fact]
    public async Task LiveE2E_DirectRedisEngine_IsReachable_And_StoresData()
    {
        Assert.NotNull(_redisDb);
        var ping = await _redisDb.PingAsync();
        Assert.True(ping.TotalMilliseconds >= 0);

        var testKey = $"{LiveRedisInstancePrefix}ping_test";
        await _redisDb.StringSetAsync(testKey, "live_redis_ok", TimeSpan.FromMinutes(1));
        var value = await _redisDb.StringGetAsync(testKey);
        Assert.Equal("live_redis_ok", value.ToString());
    }

    [Fact]
    public async Task LiveE2E_GetAcademicResults_CachesInRedis_AndServesFromCacheOnSecondCall()
    {
        Authenticate(_academicAffairsId, "AcademicAffairs");

        // First call: cache miss -> loads workspace from loader -> caches in live Redis
        var response1 = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final");
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var content1 = await response1.Content.ReadAsStringAsync();
        Assert.Contains("Nguyễn Văn An", content1);

        // Verify direct in Redis engine that key exists!
        using var cacheScope = _app.Services.CreateScope();
        var cacheService = cacheScope.ServiceProvider.GetRequiredService<ICacheService>();
        var batchVer = await cacheService.GetVersionAsync(DefenseCouncilCacheKeys.BatchScope(_batchId));
        var globalVer = await cacheService.GetVersionAsync(DefenseCouncilCacheKeys.GlobalScope);

        var expectedKey = $"{LiveRedisInstancePrefix}{DistributedCacheService.DataKeyPrefix}{DefenseCouncilCacheKeys.AcademicResults(_batchId, "Final", batchVer, globalVer)}";
        var keyExists = await _redisDb!.KeyExistsAsync(expectedKey);
        Assert.True(keyExists, $"Expected Redis key '{expectedKey}' was not created in live Redis!");

        var ttl = await _redisDb.KeyTimeToLiveAsync(expectedKey);
        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalSeconds > 0, "Redis cache key must have positive TTL");

        // Mutate underlying repo data: clear groups
        _thesisReader.Groups.Clear();

        // Second call: MUST be served from live Redis cache despite repo being empty!
        var response2 = await _client.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final");
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var content2 = await response2.Content.ReadAsStringAsync();
        Assert.Contains("Nguyễn Văn An", content2);
        Assert.Equal(content1, content2);
    }

    [Fact]
    public async Task LiveE2E_GetAcademicResultDetail_And_Export_ShareLiveRedisWorkspaceCache()
    {
        Authenticate(_academicAffairsId, "AcademicAffairs");

        // 1. Detail endpoint with required phase parameter
        var detailResponse = await _client.GetAsync($"/api/academic-affairs/results/{_groupId}?phase=Final");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detailContent = await detailResponse.Content.ReadAsStringAsync();
        Assert.Contains("Nguyễn Văn An", detailContent);

        // 2. Export endpoint with required format
        var exportResponse = await _client.GetAsync($"/api/academic-affairs/results/export?batchId={_batchId}&format=xlsx&phase=Final");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", exportResponse.Content.Headers.ContentType?.MediaType);
        var exportBytes = await exportResponse.Content.ReadAsByteArrayAsync();
        Assert.True(exportBytes.Length > 0, "Export file must not be empty");

        // 3. Verify Redis has the cached workspace
        using var cacheScope = _app.Services.CreateScope();
        var cacheService = cacheScope.ServiceProvider.GetRequiredService<ICacheService>();
        var batchVer = await cacheService.GetVersionAsync(DefenseCouncilCacheKeys.BatchScope(_batchId));
        var globalVer = await cacheService.GetVersionAsync(DefenseCouncilCacheKeys.GlobalScope);

        var expectedKey = $"{LiveRedisInstancePrefix}{DistributedCacheService.DataKeyPrefix}{DefenseCouncilCacheKeys.AcademicResults(_batchId, "Final", batchVer, globalVer)}";
        var keyExists = await _redisDb!.KeyExistsAsync(expectedKey);
        Assert.True(keyExists, $"Workspace cache key '{expectedKey}' should exist in live Redis.");
    }

    [Fact]
    public async Task LiveE2E_FullCouncilLifecycle_CachesInRedis_AndInvalidatesOnUpdate()
    {
        // 1. Create council via HTTP API (POST /api/councils)
        Authenticate(_facultyId, "Faculty");
        var createPayload = new CreateCouncilInputDto(_batchId, "HD-LIVE-01", new DateOnly(2026, 12, 20), "08:00 - 11:30", "A101");
        var createRes = await _client.PostAsJsonAsync("/api/councils", createPayload);
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var createData = await ReadDataAsync(createRes);
        var councilId = createData.GetProperty("id").GetGuid();

        // 2. Add members
        var addMembersPayload = new AddCouncilMembersInputDto(
            Members: [
                new(_chairmanId, CouncilMemberRole.Chairman),
                new(_secretaryId, CouncilMemberRole.Secretary),
                new(_memberId, CouncilMemberRole.Member)
            ]);
        var addMembersRes = await _client.PostAsJsonAsync($"/api/councils/{councilId}/members", addMembersPayload);
        Assert.Equal(HttpStatusCode.OK, addMembersRes.StatusCode);

        // 3. Assign group
        var assignGroupPayload = new AssignCouncilGroupsInputDto(Assignments: [new(_groupId, 1)]);
        var assignRes = await _client.PostAsJsonAsync($"/api/councils/{councilId}/assignments", assignGroupPayload);
        Assert.Equal(HttpStatusCode.OK, assignRes.StatusCode);
        var assignData = await ReadDataAsync(assignRes);
        var assignmentId = assignData[0].GetProperty("id").GetGuid();

        // 4. Query Councils Overview & Schedule while Draft -> Caches in Redis
        Authenticate(_facultyId, "Faculty");
        var overviewRes1 = await _client.GetAsync($"/api/councils?batchId={_batchId}");
        Assert.Equal(HttpStatusCode.OK, overviewRes1.StatusCode);
        var overviewContent1 = await overviewRes1.Content.ReadAsStringAsync();
        Assert.Contains("HD-LIVE-01", overviewContent1);

        using var cacheScope = _app.Services.CreateScope();
        var cacheService = cacheScope.ServiceProvider.GetRequiredService<ICacheService>();
        var currentBatchVer = await cacheService.GetVersionAsync(DefenseCouncilCacheKeys.BatchScope(_batchId));
        var currentGlobalVer = await cacheService.GetVersionAsync(DefenseCouncilCacheKeys.GlobalScope);

        var expectedOverviewKey = $"{LiveRedisInstancePrefix}{DistributedCacheService.DataKeyPrefix}{DefenseCouncilCacheKeys.CouncilsOverview(_batchId, "all", currentBatchVer, currentGlobalVer)}";
        Assert.True(await _redisDb!.KeyExistsAsync(expectedOverviewKey), $"Overview cache key '{expectedOverviewKey}' should exist in live Redis!");
        Assert.Equal(RedisType.Hash, await _redisDb.KeyTypeAsync(expectedOverviewKey));

        Authenticate(_chairmanId, "Lecturer");
        var schedRes1 = await _client.GetAsync($"/api/councils/schedule/{councilId}");
        Assert.Equal(HttpStatusCode.OK, schedRes1.StatusCode);
        var schedContent1 = await schedRes1.Content.ReadAsStringAsync();
        Assert.Contains("A101", schedContent1);

        var currentCouncilVer = await cacheService.GetVersionAsync(DefenseCouncilCacheKeys.CouncilScope(councilId));
        var expectedSchedKey = $"{LiveRedisInstancePrefix}{DistributedCacheService.DataKeyPrefix}{DefenseCouncilCacheKeys.CouncilSchedule(councilId, currentCouncilVer, currentBatchVer, currentGlobalVer)}";
        Assert.True(await _redisDb.KeyExistsAsync(expectedSchedKey), $"Schedule cache key '{expectedSchedKey}' should exist in live Redis!");
        Assert.Equal(RedisType.Hash, await _redisDb.KeyTypeAsync(expectedSchedKey));

        // 5. WRITE OPERATION: Update Council Schedule (PUT /api/councils/{id}) -> Triggers Cache Invalidation
        Authenticate(_facultyId, "Faculty");
        var updateSchedPayload = new UpdateCouncilScheduleInputDto(
            DefenseDate: new DateOnly(2026, 12, 25),
            TimeSlot: "13:30 - 17:00",
            Room: "B202");
        var updateSchedRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}", updateSchedPayload);
        Assert.Equal(HttpStatusCode.OK, updateSchedRes.StatusCode);

        // Verify Redis version key bumped: it should be updated to a fresh value
        var newCouncilVer = await cacheService.GetVersionAsync(DefenseCouncilCacheKeys.CouncilScope(councilId));
        Assert.NotEqual("v0", newCouncilVer);
        Assert.NotEqual(currentCouncilVer, newCouncilVer);
        Assert.True(await _redisDb.KeyExistsAsync($"{LiveRedisInstancePrefix}{DistributedCacheService.DataKeyPrefix}ver:{DefenseCouncilCacheKeys.CouncilScope(councilId)}"));

        // 6. Query Schedule again: returns FRESH data with Room B202, bypassing old cache!
        Authenticate(_chairmanId, "Lecturer");
        var schedRes2 = await _client.GetAsync($"/api/councils/schedule/{councilId}");
        Assert.Equal(HttpStatusCode.OK, schedRes2.StatusCode);
        var schedContent2 = await schedRes2.Content.ReadAsStringAsync();
        Assert.Contains("B202", schedContent2);
        Assert.Contains("13:30 - 17:00", schedContent2);

        // 7. Query Overview again: returns updated room B202!
        Authenticate(_facultyId, "Faculty");
        var overviewRes2 = await _client.GetAsync($"/api/councils?batchId={_batchId}");
        Assert.Equal(HttpStatusCode.OK, overviewRes2.StatusCode);
        var overviewContent2 = await overviewRes2.Content.ReadAsStringAsync();
        Assert.Contains("B202", overviewContent2);

        // 8. Submit to Academic Affairs & Approve
        var submitRes = await _client.PostAsJsonAsync($"/api/councils/{councilId}/submit", new SubmitCouncilToAcademicAffairsInputDto(null));
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);

        Authenticate(_academicAffairsId, "AcademicAffairs");
        var approveRes = await _client.PutAsJsonAsync($"/api/councils/{councilId}/approve", new ApproveCouncilInputDto("QD-LIVE-01"));
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        // 9. Query Minute Workspace & Discrepancy -> Caches in Redis
        Authenticate(_secretaryId, "Lecturer");
        var wsRes1 = await _client.GetAsync($"/api/councils/assignments/{assignmentId}/minute-workspace");
        Assert.Equal(HttpStatusCode.OK, wsRes1.StatusCode);

        var currentAssignVer = await cacheService.GetVersionAsync(DefenseCouncilCacheKeys.AssignmentScope(assignmentId));
        var approvedCouncilVer = await cacheService.GetVersionAsync(DefenseCouncilCacheKeys.CouncilScope(councilId));
        var latestGlobalVer = await cacheService.GetVersionAsync(DefenseCouncilCacheKeys.GlobalScope);

        var expectedWsKey = $"{LiveRedisInstancePrefix}{DistributedCacheService.DataKeyPrefix}{DefenseCouncilCacheKeys.CouncilMinuteWorkspace(assignmentId, currentAssignVer, approvedCouncilVer, latestGlobalVer)}";
        Assert.True(await _redisDb.KeyExistsAsync(expectedWsKey), $"Minute workspace key '{expectedWsKey}' should exist in Redis!");

        var discRes1 = await _client.GetAsync($"/api/councils/assignments/{assignmentId}/discrepancy-check");
        Assert.Equal(HttpStatusCode.OK, discRes1.StatusCode);
        var expectedDiscKey = $"{LiveRedisInstancePrefix}{DistributedCacheService.DataKeyPrefix}{DefenseCouncilCacheKeys.CouncilDiscrepancy(assignmentId, currentAssignVer, approvedCouncilVer, latestGlobalVer)}";
        Assert.True(await _redisDb.KeyExistsAsync(expectedDiscKey), $"Discrepancy key '{expectedDiscKey}' should exist in Redis!");
    }

    [Fact]
    public async Task LiveE2E_FailOpenResilience_UnderUnreachableRedis_OperatesNormallyWithoutErrors()
    {
        // Setup a secondary TestServer with a DEAD Redis connection string (port 6399)
        var deadRedisConfig = new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = "127.0.0.1:6399,abortConnect=false,connectTimeout=500,syncTimeout=500",
            ["Redis:InstanceName"] = "dead_redis:"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(deadRedisConfig).Build();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // Register Caching with DEAD Redis (Fail-Open)
        builder.Services.AddUgtmCaching(config);

        // Register all needed services
        builder.Services.AddSingleton<ThesisGradeCalculator>();
        builder.Services.AddScoped<IGradeCalculationService, GradeCalculationService>();
        builder.Services.AddSingleton<ICouncilRepository>(_councilRepo);
        builder.Services.AddSingleton<IDefenseCouncilUnitOfWork>(_uow);
        builder.Services.AddSingleton<IKltnBatchReader>(_batchReader);
        builder.Services.AddSingleton<ICouncilUserInfoReader>(_userInfoReader);
        builder.Services.AddSingleton<IStudentInfoReader>(_studentInfoReader);
        builder.Services.AddSingleton<IStudentSearchReader>(_studentSearchReader);
        builder.Services.AddSingleton<IDefenseGroupContract>(_defenseGroupContract);
        builder.Services.AddSingleton<ITopicDefenseContract>(_topicDefenseContract);
        builder.Services.AddSingleton<IDefenseCouncilAuditOutbox>(_auditOutbox);
        builder.Services.AddSingleton<IReauthenticationVerifier>(_reauthVerifier);
        builder.Services.AddSingleton<IAcademicResultsThesisReader>(_thesisReader);
        builder.Services.AddSingleton<IScoreSummaryStore>(_scoreStore);
        builder.Services.AddSingleton<IFinalSubmissionArchiveProofContract>(_archiveProofContract);

        builder.Services.AddScoped<CouncilMinuteWorkspaceLoader>();
        builder.Services.AddSingleton<UGTM.Modules.DefenseCouncil.Application.ScoreSummaries.ICouncilMinuteWriteGuard, TestCouncilScoreWriteGuard>();
        builder.Services.AddScoped<IFinalSubmissionAuthorityReader, TestScoreAuthority>();
        builder.Services.AddSingleton<IScoreSummaryConfigurationReader, FakeMinuteConfigurationReader>();
        builder.Services.AddSingleton<ICouncilSchedulingGuard, FakeCouncilSchedulingGuard>();

        builder.Services.AddScoped<GetCouncilsOverviewHandler>();
        builder.Services.AddScoped<AcademicResultsWorkspaceLoader>();
        builder.Services.AddScoped<GetAcademicResultsHandler>();

        builder.Services.AddAuthentication(TestLiveE2EAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestLiveE2EAuthHandler>(TestLiveE2EAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        var deadApp = builder.Build();
        deadApp.UseAuthentication();
        deadApp.UseAuthorization();
        deadApp.MapCouncilEndpoints();
        deadApp.MapAcademicAffairsResultEndpoints();

        await deadApp.StartAsync();
        using var deadClient = deadApp.GetTestClient();
        deadClient.BaseAddress = new Uri("http://localhost");

        // Authenticate as Faculty
        deadClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", $"test.{_facultyId:N}.Faculty");

        // Call GET /api/councils?batchId={batchId} -> MUST NOT THROW, must return 200 OK!
        var councilRes = await deadClient.GetAsync($"/api/councils?batchId={_batchId}");
        Assert.Equal(HttpStatusCode.OK, councilRes.StatusCode);

        // Authenticate as AcademicAffairs
        deadClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", $"test.{_academicAffairsId:N}.AcademicAffairs");

        // Call GET /api/academic-affairs/results?batchId={batchId}&phase=Final -> MUST NOT THROW, must return 200 OK!
        var resultsRes = await deadClient.GetAsync($"/api/academic-affairs/results?batchId={_batchId}&phase=Final");
        Assert.Equal(HttpStatusCode.OK, resultsRes.StatusCode);
        var resultsContent = await resultsRes.Content.ReadAsStringAsync();
        Assert.Contains("Nguyễn Văn An", resultsContent);

        await deadApp.DisposeAsync();
    }

    #endregion

    #region Fakes

    private sealed class FakeAcademicResultsThesisReader : IAcademicResultsThesisReader
    {
        public List<AcademicResultGroupSnapshotContractDto> Groups { get; } = [];

        public Task<IReadOnlyList<AcademicResultGroupSnapshotContractDto>> GetGroupsByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AcademicResultGroupSnapshotContractDto>>(
                Groups.Where(g => g.BatchId == batchId).ToList());
        }

        public Task<AcademicResultGroupSnapshotContractDto?> GetGroupByIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Groups.FirstOrDefault(g => g.GroupId == groupId));
        }
    }

    private sealed class FakeStudentSearchReader : IStudentSearchReader
    {
        public Task<IReadOnlyList<Guid>> FindIdsByNameOrCodeAsync(string keyword, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Guid>>([]);
        }
    }

    private sealed class FakeScoreSummaryStore : IScoreSummaryStore
    {
        public Dictionary<Guid, ScoreFacts> Facts { get; } = new();
        public Dictionary<Guid, ScoreHistory> Histories { get; } = new();

        public Task<IReadOnlyDictionary<Guid, ScoreFacts>> FactsAsync(IReadOnlyCollection<Guid> groups, bool locked, CancellationToken token)
        {
            var result = groups.ToDictionary(g => g, g => Facts.TryGetValue(g, out var f) ? f : new ScoreFacts([], [], [], [], [], [], [], [], []));
            return Task.FromResult<IReadOnlyDictionary<Guid, ScoreFacts>>(result);
        }

        public Task<IReadOnlyDictionary<Guid, ScoreHistory>> HistoriesAsync(IReadOnlyCollection<Guid> groups, CancellationToken token)
        {
            var result = groups.Where(Histories.ContainsKey).ToDictionary(g => g, g => Histories[g]);
            return Task.FromResult<IReadOnlyDictionary<Guid, ScoreHistory>>(result);
        }

        public Task KeyLockAsync(Guid actor, string operation, Guid key, CancellationToken token) => Task.CompletedTask;
        public Task<ScoreSummaryIdempotency?> ReceiptAsync(Guid actor, string operation, Guid key, CancellationToken token) => Task.FromResult<ScoreSummaryIdempotency?>(null);
        public Task LockAdvisorsAsync(IReadOnlyCollection<Guid> groups, CancellationToken token) => Task.CompletedTask;
        public Task<ScoreSummary?> SummaryAsync(Guid group, bool locked, CancellationToken token) => Task.FromResult(Histories.TryGetValue(group, out var h) ? h.Summary : null);
        public Task<Guid?> AssignmentGroupAsync(Guid assignment, CancellationToken token) => Task.FromResult<Guid?>(null);
        public Task<IReadOnlyList<Guid>> CouncilGroupsAsync(Guid council, CancellationToken token) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<bool> PublishedAsync(IReadOnlyCollection<Guid> groups, CancellationToken token) => Task.FromResult(false);
        public void Add(ScoreSummary summary) { }
        public void Add(ScoreSummaryRevision revision, IReadOnlyList<ScoreSummaryComponent> components) { }
        public void Add(ScoreSummaryIdempotency receipt) { }
    }

    private sealed class FakeArchiveProofContract : IFinalSubmissionArchiveProofContract
    {
        public Dictionary<Guid, FinalSubmissionArchiveProofContractDto> Proofs { get; } = new();

        public Task<FinalSubmissionArchiveProofContractDto> GetArchiveProofAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Proofs.TryGetValue(groupId, out var p) ? p : new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.Missing, groupId, Guid.Empty, null, null, null, null, null, null, null, null, null, null, null, []));
        }

        public Task<IReadOnlyList<FinalSubmissionArchiveProofContractDto>> GetManyArchiveProofsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken = default)
        {
            var list = groupIds.Select(id => Proofs.TryGetValue(id, out var p) ? p : new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.Missing, id, Guid.Empty, null, null, null, null, null, null, null, null, null, null, null, [])).ToList();
            return Task.FromResult<IReadOnlyList<FinalSubmissionArchiveProofContractDto>>(list);
        }

        public Task<FinalSubmissionArchiveProofContractDto> LockArchiveProofForCalculationAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            return GetArchiveProofAsync(groupId, cancellationToken);
        }
    }

    private sealed class TestLiveE2EAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "DefenseCouncilLiveE2ETestScheme";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer test.", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var parts = header["Bearer test.".Length..].Split('.');
            if (parts.Length < 2 || !Guid.TryParseExact(parts[0], "N", out var userId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid test bearer token."));
            }

            var role = parts[1];
            var claims = new List<Claim>
            {
                new("sub", userId.ToString()),
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Role, role),
                new("role", role)
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    #endregion
}
