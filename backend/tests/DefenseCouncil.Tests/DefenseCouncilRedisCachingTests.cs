using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Infrastructure.Caching;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Common;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.ExportAcademicResults;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResultDetail;
using UGTM.Modules.DefenseCouncil.Application.AcademicResults.GetAcademicResults;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.ApproveCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.AssignCouncilGroups;
using UGTM.Modules.DefenseCouncil.Application.Councils.CheckCouncilDiscrepancy;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.CreateCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMinuteWorkspace;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMemberCandidates;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilsOverview;
using UGTM.Modules.DefenseCouncil.Application.Councils.LockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.RemoveCouncilMember;
using UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilScore;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilToAcademicAffairs;
using UGTM.Modules.DefenseCouncil.Application.Councils.UnlockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Infrastructure.Exporting;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class DefenseCouncilRedisCachingTests
{
    #region Infrastructure: Options, Registration, DistributedCacheService & Fail-Open

    [Fact]
    public void RedisCacheOptions_ResolvesAndNormalizesConnectionString()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "redis:6379,password=ugtm_redis_pass_123"
            })
            .Build();

        var resolved = UGTM.BuildingBlocks.Infrastructure.Caching.RedisCacheOptions.ResolveConnectionString(config);

        Assert.NotNull(resolved);
        Assert.Contains("redis:6379,password=ugtm_redis_pass_123", resolved, StringComparison.Ordinal);
        Assert.Contains("abortConnect=false", resolved, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("connectTimeout=2000", resolved, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("syncTimeout=2000", resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedisCacheOptions_DoesNotDuplicateExistingResilienceFlags()
    {
        var raw = "localhost:6379,abortConnect=true,connectTimeout=5000,syncTimeout=5000";
        var normalized = UGTM.BuildingBlocks.Infrastructure.Caching.RedisCacheOptions.NormalizeConnectionString(raw);

        Assert.Equal(raw, normalized);
    }

    [Fact]
    public void AddUgtmCaching_FallsBackToMemoryDistributedCache_WhenRedisNotConfigured()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var services = new ServiceCollection().AddUgtmCaching(config);

        using var provider = services.BuildServiceProvider();
        var distributedCache = provider.GetRequiredService<IDistributedCache>();
        var cacheService = provider.GetRequiredService<ICacheService>();

        Assert.IsType<MemoryDistributedCache>(distributedCache);
        Assert.IsType<DistributedCacheService>(cacheService);
    }

    [Fact]
    public void AddUgtmCaching_RegistersRedisCache_WhenConnectionStringConfigured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "localhost:6379,password=secret"
            })
            .Build();

        var services = new ServiceCollection().AddUgtmCaching(config);

        using var provider = services.BuildServiceProvider();
        var distributedCache = provider.GetRequiredService<IDistributedCache>();
        var cacheService = provider.GetRequiredService<ICacheService>();

        Assert.IsAssignableFrom<RedisCache>(distributedCache);
        Assert.IsType<DistributedCacheService>(cacheService);
    }

    [Fact]
    public void AddUgtmCaching_IsIdempotent_AndPreservesPreRegisteredDistributedCache()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "localhost:6379"
            })
            .Build();

        var customCache = new FaultyDistributedCache();
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache>(customCache);
        services.AddUgtmCaching(config);
        services.AddUgtmCaching(config);

        using var provider = services.BuildServiceProvider();
        Assert.Same(customCache, provider.GetRequiredService<IDistributedCache>());
    }

    [Fact]
    public async Task DistributedCacheService_RoundTripsJsonAndVersioning_Correctly()
    {
        var memory = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = new DistributedCacheService(memory);

        var dto = new CouncilOverviewItemDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "HD01",
            "Khoa CNTT",
            "N01",
            "AI System",
            "A101",
            "08:00 - 11:30",
            new DateOnly(2026, 12, 20),
            "Draft",
            "Chờ phê duyệt",
            "IndependentMember",
            "CT · TK · UV độc lập",
            "Pending",
            "Chưa có phiếu",
            "Open",
            "Chưa khóa",
            null,
            null,
            null,
            []);

        await service.SetAsync("test:overview", dto, TimeSpan.FromMinutes(2));
        var cached = await service.GetAsync<CouncilOverviewItemDto>("test:overview");
        Assert.NotNull(cached);
        Assert.Equal(dto.Id, cached.Id);
        Assert.Equal(dto.CouncilCode, cached.CouncilCode);
        Assert.Equal(dto.DefenseDate, cached.DefenseDate);
        Assert.Equal(dto.Members, cached.Members);

        await service.RemoveAsync("test:overview");
        Assert.Null(await service.GetAsync<CouncilOverviewItemDto>("test:overview"));

        var initialVer = await service.GetVersionAsync("dc:batch:1");
        Assert.Equal("v0", initialVer);

        await service.BumpVersionAsync("dc:batch:1");
        var bumpedVer = await service.GetVersionAsync("dc:batch:1");
        Assert.NotEqual("v0", bumpedVer);

        await service.BumpVersionAsync("dc:batch:1");
        var bumpedVer2 = await service.GetVersionAsync("dc:batch:1");
        Assert.NotEqual(bumpedVer, bumpedVer2);
    }

    [Fact]
    public async Task DistributedCacheService_FailsOpen_WhenDistributedCacheThrows()
    {
        var service = new DistributedCacheService(new FaultyDistributedCache());

        // Must never throw on Redis outage; GET returns null, GET VERSION returns unique transient string
        var cached = await service.GetAsync<CouncilOverviewItemDto>("any-key");
        Assert.Null(cached);

        await service.SetAsync("any-key", new AcademicResultListItemDto(
            Guid.NewGuid(), "N01", Guid.NewGuid(), "Topic", null, [], [], "Khoa CNTT", "Final", "PUBLISHED", "Khoa đã công bố", "Lưu chiểu đạt", 9.0m));
        await service.RemoveAsync("any-key");
        await service.BumpVersionAsync("scope-1");

        var ver1 = await service.GetVersionAsync("scope-1");
        var ver2 = await service.GetVersionAsync("scope-1");
        Assert.NotEqual(ver1, ver2);
    }

    [Fact]
    public async Task DistributedCacheService_PropagatesOperationCanceledException_WhenTokenCancelled()
    {
        var service = new DistributedCacheService(new FaultyDistributedCache());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetAsync<string>("any-key", cts.Token));
    }

    #endregion

    #region AcademicResults Cache Hit, Reuse Across List/Detail/Export & Invalidation

    [Fact]
    public async Task AcademicResults_CachesBatchWorkspace_AndReusesAcrossListDetailExport_UntilInvalidated()
    {
        var batchId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();

        var thesisReader = new CountingAcademicResultsThesisReader();
        thesisReader.Groups.Add(new AcademicResultGroupSnapshotContractDto(
            groupId,
            topicId,
            batchId,
            "InProgress",
            DateTime.UtcNow.AddDays(-10),
            [new DefenseGroupMemberContractDto(studentId, batchId, "Confirmed")],
            new AcademicResultMidtermSnapshotContractDto(Guid.NewGuid(), "Approved", 1, "OK", advisorId, DateTime.UtcNow, DateTime.UtcNow),
            "ArchiveConfirmed"));

        var topicContract = new FakeTopicDefenseContract();
        topicContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, batchId, advisorId, "Đề tài ban đầu", "Initial Topic");

        var userInfoReader = new FakeCouncilUserInfoReader();
        userInfoReader.Users[studentId] = new CouncilUserInfoContractDto(studentId, "Nguyễn Văn An", "an@uit.edu.vn", "22520001", true);
        userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "TS. GVHD", "gvhd@uit.edu.vn", "GV01", true);

        var councilRepo = new FakeCouncilRepository();
        var scoreStore = new EmptyScoreSummaryStore();
        var archiveProof = new EmptyArchiveProofContract();
        var studentSearch = new FakeStudentSearchReader();
        var currentUser = new FakeCurrentUser { Roles = new HashSet<string> { "AcademicAffairs" } };

        var memory = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var cache = new DistributedCacheService(memory);

        var loader = new AcademicResultsWorkspaceLoader(
            thesisReader,
            topicContract,
            userInfoReader,
            studentSearch,
            councilRepo,
            scoreStore,
            archiveProof,
            cache);

        var listHandler = new GetAcademicResultsHandler(loader, currentUser);
        var detailHandler = new GetAcademicResultDetailHandler(loader, currentUser);
        var exportHandler = new ExportAcademicResultsHandler(loader, new AcademicResultsExportFileGenerator(), currentUser);

        // 1. First list call -> Cache Miss -> queries thesisReader
        var firstList = await listHandler.HandleAsync(new GetAcademicResultsQuery(batchId, "Final"));
        Assert.True(firstList.IsSuccess);
        Assert.Equal("Đề tài ban đầu", firstList.Value.Items.Single().TopicTitleVi);
        Assert.Equal(1, thesisReader.GetGroupsByBatchCallCount);

        // Mutate underlying data without invalidating cache yet
        topicContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, batchId, advisorId, "Đề tài đã cập nhật", "Updated Topic");

        // 2. Subsequent list with filter, detail, and export calls MUST hit cache (still 1 DB call for batch)
        var filteredList = await listHandler.HandleAsync(new GetAcademicResultsQuery(batchId, "Final", Keyword: "ban đầu", PageIndex: 1, PageSize: 10));
        Assert.True(filteredList.IsSuccess);
        Assert.Single(filteredList.Value.Items);
        Assert.Equal(1, thesisReader.GetGroupsByBatchCallCount);

        var detailResult = await detailHandler.HandleAsync(new GetAcademicResultDetailQuery(groupId, "Final"));
        Assert.True(detailResult.IsSuccess);
        Assert.Equal("Đề tài ban đầu", detailResult.Value.TopicTitleVi);
        Assert.Equal(1, thesisReader.GetGroupsByBatchCallCount);
        Assert.Equal(1, thesisReader.GetGroupByIdCallCount);

        // Second detail call reuses GroupBatchMap cache too!
        var detailResult2 = await detailHandler.HandleAsync(new GetAcademicResultDetailQuery(groupId, "Final"));
        Assert.True(detailResult2.IsSuccess);
        Assert.Equal(1, thesisReader.GetGroupByIdCallCount);

        var exportResult = await exportHandler.HandleAsync(new ExportAcademicResultsQuery(batchId, "xlsx", "Final"));
        Assert.True(exportResult.IsSuccess);
        Assert.NotEmpty(exportResult.Value.FileBytes);
        Assert.Equal(1, thesisReader.GetGroupsByBatchCallCount);

        // 3. Invalidate batch cache -> next call fetches fresh data!
        await DefenseCouncilCacheKeys.InvalidateBatchAsync(cache, batchId);

        var refreshedList = await listHandler.HandleAsync(new GetAcademicResultsQuery(batchId, "Final"));
        Assert.True(refreshedList.IsSuccess);
        Assert.Equal("Đề tài đã cập nhật", refreshedList.Value.Items.Single().TopicTitleVi);
        Assert.Equal(2, thesisReader.GetGroupsByBatchCallCount);

        var refreshedDetail = await detailHandler.HandleAsync(new GetAcademicResultDetailQuery(groupId, "Final"));
        Assert.True(refreshedDetail.IsSuccess);
        Assert.Equal("Đề tài đã cập nhật", refreshedDetail.Value.TopicTitleVi);
    }

    #endregion

    #region Councils: Overview, Schedule, MinuteWorkspace, Discrepancy, RBAC Isolation & Write Invalidation

    [Fact]
    public async Task CouncilsOverview_And_Schedule_CacheData_PreserveStudentRbac_AndInvalidateOnMutations()
    {
        var batchId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var chairmanId = Guid.NewGuid();
        var secretaryId = Guid.NewGuid();

        var innerRepo = new FakeCouncilRepository();
        var councilRepo = new CountingCouncilRepository(innerRepo);
        var uow = new FakeDefenseCouncilUnitOfWork();
        var batchReader = new FakeBatchReader();
        batchReader.ExistingBatches.Add(batchId);

        var userInfoReader = new FakeCouncilUserInfoReader();
        userInfoReader.Users[chairmanId] = new CouncilUserInfoContractDto(chairmanId, "PGS. Chủ Tịch", "ct@uit.edu.vn", "GV01", true);
        userInfoReader.Users[secretaryId] = new CouncilUserInfoContractDto(secretaryId, "ThS. Thư Ký", "tk@uit.edu.vn", "GV02", true);
        userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "TS. GVHD", "hd@uit.edu.vn", "GV03", true);

        var studentInfoReader = new FakeStudentInfoReader();
        studentInfoReader.Students[studentId] = new StudentInfoContractDto(studentId, "an@uit.edu.vn", "Nguyễn Văn An", "22520001", null);

        var groupContract = new FakeDefenseGroupContract();
        groupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId, topicId, batchId, "InProgress", DateTime.UtcNow,
            [new DefenseGroupMemberContractDto(studentId, batchId, "Confirmed")]);

        var topicContract = new FakeTopicDefenseContract();
        topicContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, batchId, advisorId, "Đề tài AI", "AI Topic");

        var currentUser = new FakeCurrentUser
        {
            UserId = chairmanId,
            Roles = new HashSet<string> { "Faculty" }
        };

        var memory = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var cache = new DistributedCacheService(memory);
        var schedulingGuard = new FakeCouncilSchedulingGuard();
        var auditOutbox = new FakeDefenseCouncilAuditOutbox();

        var overviewHandler = new GetCouncilsOverviewHandler(councilRepo, groupContract, topicContract, userInfoReader, currentUser, cache);
        var scheduleHandler = new GetCouncilScheduleHandler(councilRepo, groupContract, topicContract, userInfoReader, studentInfoReader, currentUser, cache);
        var createHandler = new CreateCouncilHandler(councilRepo, uow, batchReader, currentUser, cache);
        var addMembersHandler = new AddCouncilMembersHandler(councilRepo, uow, userInfoReader, groupContract, topicContract, currentUser, cache);
        var assignGroupsHandler = new AssignCouncilGroupsHandler(councilRepo, uow, groupContract, topicContract, userInfoReader, currentUser, schedulingGuard, cache);
        var updateScheduleHandler = new UpdateCouncilScheduleHandler(councilRepo, uow, userInfoReader, currentUser, cache);
        var submitToAaHandler = new SubmitCouncilToAcademicAffairsHandler(councilRepo, uow, groupContract, topicContract, userInfoReader, auditOutbox, currentUser, schedulingGuard, cache);
        var approveHandler = new ApproveCouncilHandler(councilRepo, uow, groupContract, topicContract, userInfoReader, auditOutbox, currentUser, schedulingGuard, cache);

        // 1. Create Council + Add Members + Assign Group
        var created = await createHandler.HandleAsync(new CreateCouncilCommand(batchId, "HD-01", new DateOnly(2026, 12, 20), "08:00 - 11:30", "A101"));
        Assert.True(created.IsSuccess);
        var councilId = created.Value.Id;

        var addedMembers = await addMembersHandler.HandleAsync(new AddCouncilMembersCommand(
            councilId,
            [
                new CouncilMemberInputDto(chairmanId, CouncilMemberRole.Chairman),
                new CouncilMemberInputDto(secretaryId, CouncilMemberRole.Secretary)
            ]));
        Assert.True(addedMembers.IsSuccess);

        var assigned = await assignGroupsHandler.HandleAsync(new AssignCouncilGroupsCommand(
            councilId,
            [new CouncilAssignmentInputDto(groupId, 1)]));
        Assert.True(assigned.IsSuccess);

        // 2. Overview Hit & Miss
        var ov1 = await overviewHandler.HandleAsync(new GetCouncilsOverviewQuery(batchId, null, null, null, 1, 50));
        Assert.True(ov1.IsSuccess);
        Assert.Single(ov1.Value);
        Assert.Equal("A101", ov1.Value[0].Room);
        Assert.Equal(1, councilRepo.OverviewCallCount);

        var ov2 = await overviewHandler.HandleAsync(new GetCouncilsOverviewQuery(batchId, "HD-01", null, null, 1, 10));
        Assert.True(ov2.IsSuccess);
        Assert.Single(ov2.Value);
        Assert.Equal(1, councilRepo.OverviewCallCount); // Hit cache!

        // 3. Schedule Hit & Student RBAC isolation on Draft council
        var sch1 = await scheduleHandler.HandleAsync(new GetCouncilScheduleQuery(councilId));
        Assert.True(sch1.IsSuccess);
        Assert.Equal("A101", sch1.Value.Room);

        var userReadsBeforeHit = councilRepo.ActiveReviewerBatchCalls;
        var sch2 = await scheduleHandler.HandleAsync(new GetCouncilScheduleQuery(councilId));
        Assert.True(sch2.IsSuccess);
        Assert.Equal(userReadsBeforeHit, councilRepo.ActiveReviewerBatchCalls); // Cached!

        // Switch user to Student -> MUST still receive 403 ScheduleNotPublished even though Faculty cached the schedule!
        currentUser.UserId = studentId;
        currentUser.Roles = new HashSet<string> { "Student" };
        var studentDraftSchedule = await scheduleHandler.HandleAsync(new GetCouncilScheduleQuery(councilId));
        Assert.True(studentDraftSchedule.IsFailure);
        Assert.Equal("Council.ScheduleNotPublished", studentDraftSchedule.Error.Code);

        // 4. Update schedule as Faculty -> automatically invalidates Overview & Schedule cache
        currentUser.UserId = chairmanId;
        currentUser.Roles = new HashSet<string> { "Faculty" };
        var updated = await updateScheduleHandler.HandleAsync(new UpdateCouncilScheduleCommand(councilId, new DateOnly(2026, 12, 21), "13:30 - 17:00", "B202"));
        Assert.True(updated.IsSuccess);

        var ov3 = await overviewHandler.HandleAsync(new GetCouncilsOverviewQuery(batchId, null, null, null, 1, 50));
        Assert.True(ov3.IsSuccess);
        Assert.Equal("B202", ov3.Value[0].Room);
        Assert.Equal(2, councilRepo.OverviewCallCount);

        var sch3 = await scheduleHandler.HandleAsync(new GetCouncilScheduleQuery(councilId));
        Assert.True(sch3.IsSuccess);
        Assert.Equal("B202", sch3.Value.Room);

        // 5. Submit & Approve as AcademicAffairs -> invalidates cache so Student can now see Active schedule
        var submitted = await submitToAaHandler.HandleAsync(new SubmitCouncilToAcademicAffairsCommand(councilId, "Sẵn sàng"));
        Assert.True(submitted.IsSuccess);

        currentUser.Roles = new HashSet<string> { "AcademicAffairs" };
        var approved = await approveHandler.HandleAsync(new ApproveCouncilCommand(councilId, "123/QD", new DateOnly(2026, 12, 1)));
        Assert.True(approved.IsSuccess);

        currentUser.UserId = studentId;
        currentUser.Roles = new HashSet<string> { "Student" };
        var studentActiveSchedule = await scheduleHandler.HandleAsync(new GetCouncilScheduleQuery(councilId));
        Assert.True(studentActiveSchedule.IsSuccess);
        Assert.Equal("Active", studentActiveSchedule.Value.Status);
        Assert.Equal("B202", studentActiveSchedule.Value.Room);
    }

    [Fact]
    public async Task CouncilMinuteWorkspace_And_Discrepancy_CacheData_IgnorePreview_AndInvalidateOnScoreAndLockLifecycle()
    {
        var batchId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var chairmanUserId = Guid.NewGuid();
        var secretaryUserId = Guid.NewGuid();

        var innerRepo = new FakeCouncilRepository();
        var councilRepo = new CountingCouncilRepository(innerRepo);
        var uow = new FakeDefenseCouncilUnitOfWork();

        var council = Council.Create(batchId, "HD02", new DateOnly(2026, 12, 22), "08:00 - 11:30", "C303");
        council.SubmitToAcademicAffairs(Guid.NewGuid(), null, DateTime.UtcNow);
        council.Approve("456/QD", new DateOnly(2026, 12, 2), null, Guid.NewGuid(), DateTime.UtcNow);
        innerRepo.Councils.Add(council);

        var reviewerId = Guid.NewGuid();
        var chairmanMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = chairmanUserId, RoleInCouncil = CouncilMemberRole.Chairman };
        var secretaryMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = secretaryUserId, RoleInCouncil = CouncilMemberRole.Secretary };
        var reviewerMember = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = reviewerId, RoleInCouncil = CouncilMemberRole.Member };
        innerRepo.Members.AddRange([chairmanMember, secretaryMember, reviewerMember]);

        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = groupId, OrderNumber = 1 };
        innerRepo.Assignments.Add(assignment);

        var criteria = new GradingCriteria { Id = Guid.NewGuid(), BatchId = batchId, CriteriaCode = "C1", CriteriaName = "Nội dung", MaxScore = 10.0m, OrderIndex = 1 };
        innerRepo.GradingCriteriaList.Add(criteria);

        // Existing advisor & reviewer scores (GVPB kiêm UV -> mô hình chia 6) so workspace is ready to lock once Chairman & Secretary score
        innerRepo.AdvisorEvaluations[groupId] = AdvisorEvaluation.Create(
            Guid.NewGuid(),
            groupId,
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            8.0m,
            "Hướng dẫn tốt",
            advisorId,
            DateTime.UtcNow);

        var revAssignment = ReviewerAssignment.Create(
            Guid.NewGuid(),
            groupId,
            reviewerId,
            advisorId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Guid.NewGuid(),
            DateTime.UtcNow);
        var revEvaluation = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), revAssignment.Id, 8.0m, "Phản biện tốt", DefenseApprovalStatus.Approved, reviewerId, DateTime.UtcNow);
        revAssignment.GetType().GetProperty("Evaluation")?.SetValue(revAssignment, revEvaluation);
        innerRepo.ActiveReviewerAssignments[groupId] = revAssignment;

        var userInfoReader = new FakeCouncilUserInfoReader();
        userInfoReader.Users[chairmanUserId] = new CouncilUserInfoContractDto(chairmanUserId, "PGS. Chủ Tịch", "ct@uit.edu.vn", "GV01", true);
        userInfoReader.Users[secretaryUserId] = new CouncilUserInfoContractDto(secretaryUserId, "ThS. Thư Ký", "tk@uit.edu.vn", "GV02", true);
        userInfoReader.Users[advisorId] = new CouncilUserInfoContractDto(advisorId, "TS. GVHD", "hd@uit.edu.vn", "GV03", true);
        userInfoReader.Users[reviewerId] = new CouncilUserInfoContractDto(reviewerId, "TS. GVPB", "pb@uit.edu.vn", "GV04", true);

        var groupContract = new FakeDefenseGroupContract();
        groupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(groupId, topicId, batchId, "InProgress", DateTime.UtcNow, []);

        var topicContract = new FakeTopicDefenseContract();
        topicContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, batchId, advisorId, "Đề tài Web", "Web Topic");

        var currentUser = new FakeCurrentUser
        {
            UserId = secretaryUserId,
            Roles = new HashSet<string> { "Lecturer", "Faculty" },
            Permissions = new HashSet<string> { PermissionCodes.CouncilMinutesUnlock }
        };

        var memory = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var cache = new DistributedCacheService(memory);
        var configReader = new FakeMinuteConfigurationReader();
        var workspaceLoader = new CouncilMinuteWorkspaceLoader(councilRepo, userInfoReader, groupContract, topicContract, configReader);
        var writeGuard = new TestCouncilScoreWriteGuard();
        var authority = new TestScoreAuthority(currentUser);
        var audit = new FakeDefenseCouncilAuditOutbox();
        var reauth = new FakeReauthenticationVerifier();

        var getWorkspaceHandler = new GetCouncilMinuteWorkspaceHandler(councilRepo, workspaceLoader, currentUser, cache);
        var discrepancyHandler = new CheckCouncilDiscrepancyHandler(councilRepo, userInfoReader, currentUser, authority, cache);
        var submitScoreHandler = new SubmitCouncilScoreHandler(councilRepo, uow, currentUser, writeGuard, TimeProvider.System, cache);
        var saveMinuteHandler = new SaveCouncilMinuteHandler(councilRepo, uow, workspaceLoader, currentUser, writeGuard, authority, audit, TimeProvider.System, cache);
        var lockMinuteHandler = new LockCouncilMinuteHandler(councilRepo, uow, workspaceLoader, reauth, audit, currentUser, writeGuard, authority, TimeProvider.System, cache);
        var unlockMinuteHandler = new UnlockCouncilMinuteHandler(councilRepo, uow, authority, audit, currentUser, writeGuard, TimeProvider.System, cache);

        // 1. Initial read -> Miss -> caches Workspace & Discrepancy
        var ws1 = await getWorkspaceHandler.HandleAsync(new GetCouncilMinuteWorkspaceQuery(assignment.Id));
        var disc1 = await discrepancyHandler.HandleAsync(new CheckCouncilDiscrepancyQuery(assignment.Id));
        Assert.True(ws1.IsSuccess);
        Assert.True(disc1.IsSuccess);
        Assert.Null(disc1.Value.AverageCouncilScore);
        var scoreQueriesAfterFirstRead = councilRepo.CouncilScoresQueryCount;

        // 2. Second read -> Hit cache (CouncilScoresQueryCount does not increase!)
        var ws2 = await getWorkspaceHandler.HandleAsync(new GetCouncilMinuteWorkspaceQuery(assignment.Id));
        var disc2 = await discrepancyHandler.HandleAsync(new CheckCouncilDiscrepancyQuery(assignment.Id));
        Assert.True(ws2.IsSuccess);
        Assert.True(disc2.IsSuccess);
        Assert.Equal(scoreQueriesAfterFirstRead, councilRepo.CouncilScoresQueryCount);

        // 3. Preview save -> MUST NOT invalidate cache
        var previewSave = await saveMinuteHandler.HandleAsync(new SaveCouncilMinuteCommand(
            assignment.Id, null, null, "Nhận xét nháp preview", null, IsPreview: true));
        Assert.True(previewSave.IsSuccess);

        var queriesAfterPreview = councilRepo.CouncilScoresQueryCount;
        var wsAfterPreview = await getWorkspaceHandler.HandleAsync(new GetCouncilMinuteWorkspaceQuery(assignment.Id));
        Assert.True(wsAfterPreview.IsSuccess);
        Assert.Equal(queriesAfterPreview, councilRepo.CouncilScoresQueryCount);

        // 4. Submit Council Score (Secretary = 8.5, Chairman = 9.0) -> MUST invalidate cache!
        var s1 = await submitScoreHandler.HandleAsync(new SubmitCouncilScoreCommand(
            assignment.Id, [new CriterionScoreInputDto(criteria.Id, 8.5m)], "Tốt"));
        Assert.True(s1.IsSuccess);

        currentUser.UserId = chairmanUserId;
        var s2 = await submitScoreHandler.HandleAsync(new SubmitCouncilScoreCommand(
            assignment.Id, [new CriterionScoreInputDto(criteria.Id, 9.0m)], "Xuất sắc"));
        Assert.True(s2.IsSuccess);

        // Next read sees fresh average score (8.75) immediately!
        var discAfterScore = await discrepancyHandler.HandleAsync(new CheckCouncilDiscrepancyQuery(assignment.Id));
        Assert.True(discAfterScore.IsSuccess);
        Assert.Equal(8.75m, discAfterScore.Value.AverageCouncilScore);
        Assert.True(councilRepo.CouncilScoresQueryCount > scoreQueriesAfterFirstRead);

        // 5. Real Save Minute (not preview) -> invalidates cache and updates feedback
        currentUser.UserId = secretaryUserId;
        var realSave = await saveMinuteHandler.HandleAsync(new SaveCouncilMinuteCommand(
            assignment.Id, null, null, "Nhận xét chính thức của Hội đồng", null, IsPreview: false));
        Assert.True(realSave.IsSuccess);

        var wsAfterSave = await getWorkspaceHandler.HandleAsync(new GetCouncilMinuteWorkspaceQuery(assignment.Id));
        Assert.True(wsAfterSave.IsSuccess);
        Assert.Equal("Nhận xét chính thức của Hội đồng", wsAfterSave.Value.AssignmentInfo.StudentFeedback);

        // 6. Lock Minute -> invalidates cache so Workspace shows LockStatus = Locked
        var locked = await lockMinuteHandler.HandleAsync(new LockCouncilMinuteCommand(assignment.Id, "valid-password"));
        Assert.True(locked.IsSuccess, $"{locked.Error?.Code}: {locked.Error?.Message}");

        var wsAfterLock = await getWorkspaceHandler.HandleAsync(new GetCouncilMinuteWorkspaceQuery(assignment.Id));
        Assert.True(wsAfterLock.IsSuccess);
        Assert.Equal("Locked", wsAfterLock.Value.AssignmentInfo.LockStatus);

        // 7. Unlock Minute -> invalidates cache so Workspace shows UnlockedByOverride
        var unlocked = await unlockMinuteHandler.HandleAsync(new UnlockCouncilMinuteCommand(assignment.Id, "Cần mở khóa để cập nhật điểm phản biện"));
        Assert.True(unlocked.IsSuccess);

        var wsAfterUnlock = await getWorkspaceHandler.HandleAsync(new GetCouncilMinuteWorkspaceQuery(assignment.Id));
        Assert.True(wsAfterUnlock.IsSuccess);
        Assert.Equal("UnlockedByOverride", wsAfterUnlock.Value.AssignmentInfo.LockStatus);
    }

    [Fact]
    public async Task AllCachedHandlers_OperateNormally_WhenRedisIsUnavailable_FailOpen()
    {
        var batchId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();

        var faultyCache = new DistributedCacheService(new FaultyDistributedCache());

        var thesisReader = new CountingAcademicResultsThesisReader();
        thesisReader.Groups.Add(new AcademicResultGroupSnapshotContractDto(
            groupId, topicId, batchId, "InProgress", DateTime.UtcNow, [], null, null));

        var topicContract = new FakeTopicDefenseContract();
        topicContract.Topics[topicId] = new TopicDefenseDetailsContractDto(topicId, batchId, advisorId, "Đề tài Fail-Open", null);

        var innerRepo = new FakeCouncilRepository();
        var uow = new FakeDefenseCouncilUnitOfWork();
        var batchReader = new FakeBatchReader();
        batchReader.ExistingBatches.Add(batchId);
        var currentUser = new FakeCurrentUser { Roles = new HashSet<string> { "Admin" } };

        var loader = new AcademicResultsWorkspaceLoader(
            thesisReader,
            topicContract,
            new FakeCouncilUserInfoReader(),
            new FakeStudentSearchReader(),
            innerRepo,
            new EmptyScoreSummaryStore(),
            new EmptyArchiveProofContract(),
            faultyCache);

        var resultsHandler = new GetAcademicResultsHandler(loader, currentUser);
        var overviewHandler = new GetCouncilsOverviewHandler(innerRepo, new FakeDefenseGroupContract(), topicContract, new FakeCouncilUserInfoReader(), currentUser, faultyCache);
        var createHandler = new CreateCouncilHandler(innerRepo, uow, batchReader, currentUser, faultyCache);

        // Even with FaultyDistributedCache throwing on every call, read and write handlers succeed!
        var created = await createHandler.HandleAsync(new CreateCouncilCommand(batchId, "HD-FAILOPEN", new DateOnly(2026, 12, 28), "08:00 - 11:30", "E101"));
        Assert.True(created.IsSuccess);

        var results = await resultsHandler.HandleAsync(new GetAcademicResultsQuery(batchId, "Final"));
        Assert.True(results.IsSuccess);
        Assert.Single(results.Value.Items);
        Assert.Equal("Đề tài Fail-Open", results.Value.Items[0].TopicTitleVi);

        var overview = await overviewHandler.HandleAsync(new GetCouncilsOverviewQuery(batchId, null, null, null, 1, 20));
        Assert.True(overview.IsSuccess);
        Assert.Single(overview.Value);
        Assert.Equal("HD-FAILOPEN", overview.Value[0].CouncilCode);
    }

    #endregion

    #region Test Doubles

    private sealed class FaultyDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("Redis connection refused");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => token.IsCancellationRequested ? Task.FromCanceled<byte[]?>(token) : throw new InvalidOperationException("Redis connection refused");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new InvalidOperationException("Redis connection refused");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : throw new InvalidOperationException("Redis connection refused");
        public void Refresh(string key) => throw new InvalidOperationException("Redis connection refused");
        public Task RefreshAsync(string key, CancellationToken token = default)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : throw new InvalidOperationException("Redis connection refused");
        public void Remove(string key) => throw new InvalidOperationException("Redis connection refused");
        public Task RemoveAsync(string key, CancellationToken token = default)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : throw new InvalidOperationException("Redis connection refused");
    }

    private sealed class CountingAcademicResultsThesisReader : IAcademicResultsThesisReader
    {
        public List<AcademicResultGroupSnapshotContractDto> Groups { get; } = [];
        public int GetGroupsByBatchCallCount { get; private set; }
        public int GetGroupByIdCallCount { get; private set; }

        public Task<IReadOnlyList<AcademicResultGroupSnapshotContractDto>> GetGroupsByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            GetGroupsByBatchCallCount++;
            return Task.FromResult<IReadOnlyList<AcademicResultGroupSnapshotContractDto>>(
                Groups.Where(g => g.BatchId == batchId).ToList());
        }

        public Task<AcademicResultGroupSnapshotContractDto?> GetGroupByIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            GetGroupByIdCallCount++;
            return Task.FromResult(Groups.FirstOrDefault(g => g.GroupId == groupId));
        }
    }

    private sealed class CountingCouncilRepository(FakeCouncilRepository inner) : Domain.Repositories.ICouncilRepository
    {
        public int OverviewCallCount { get; private set; }
        public int ActiveReviewerBatchCalls { get; private set; }
        public int CouncilScoresQueryCount { get; private set; }

        public Task<Council?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<Council?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) => inner.GetByIdWithDetailsAsync(id, ct);
        public Task<IReadOnlyList<Council>> GetCouncilsForOverviewAsync(Guid? batchId = null, CouncilStatus? status = null, CancellationToken ct = default)
        {
            OverviewCallCount++;
            return inner.GetCouncilsForOverviewAsync(batchId, status, ct);
        }
        public Task<bool> ExistsByCodeInBatchAsync(Guid batchId, string councilCode, Guid? excludeCouncilId = null, CancellationToken ct = default)
            => inner.ExistsByCodeInBatchAsync(batchId, councilCode, excludeCouncilId, ct);
        public Task<bool> HasRoomScheduleConflictAsync(DateOnly defenseDate, string timeSlot, string room, Guid? excludeCouncilId = null, CancellationToken ct = default)
            => inner.HasRoomScheduleConflictAsync(defenseDate, timeSlot, room, excludeCouncilId, ct);
        public Task<IReadOnlyList<Guid>> GetConflictingMemberUserIdsAsync(DateOnly defenseDate, string timeSlot, IEnumerable<Guid> userIds, Guid? excludeCouncilId = null, CancellationToken ct = default)
            => inner.GetConflictingMemberUserIdsAsync(defenseDate, timeSlot, userIds, excludeCouncilId, ct);
        public Task<IReadOnlyDictionary<Guid, Guid>> GetAssignedGroupCouncilsAsync(IEnumerable<Guid> groupIds, Guid? excludeCouncilId = null, CancellationToken ct = default)
            => inner.GetAssignedGroupCouncilsAsync(groupIds, excludeCouncilId, ct);
        public Task<IReadOnlySet<Guid>> GetRejectedDefenseGroupIdsAsync(IEnumerable<Guid> groupIds, CancellationToken ct = default)
            => inner.GetRejectedDefenseGroupIdsAsync(groupIds, ct);
        public Task<IReadOnlySet<Guid>> GetDefenseEligibleGroupIdsAsync(IEnumerable<Guid> groupIds, CancellationToken ct = default)
            => inner.GetDefenseEligibleGroupIdsAsync(groupIds, ct);
        public Task<IReadOnlySet<Guid>> LockDefenseEligibleGroupIdsAsync(IEnumerable<Guid> groupIds, CancellationToken ct = default)
            => inner.LockDefenseEligibleGroupIdsAsync(groupIds, ct);
        public Task<Council?> GetCouncilByGroupIdAsync(Guid groupId, CancellationToken ct = default) => inner.GetCouncilByGroupIdAsync(groupId, ct);
        public Task<IReadOnlyDictionary<Guid, Guid>> GetActiveReviewerIdsByGroupIdsAsync(IEnumerable<Guid> groupIds, CancellationToken ct = default)
        {
            ActiveReviewerBatchCalls++;
            return inner.GetActiveReviewerIdsByGroupIdsAsync(groupIds, ct);
        }
        public Task AddAsync(Council council, CancellationToken ct = default) => inner.AddAsync(council, ct);
        public Task AddMembersAsync(IEnumerable<CouncilMember> members, CancellationToken ct = default) => inner.AddMembersAsync(members, ct);
        public Task RemoveMemberAsync(CouncilMember member, CancellationToken ct = default) => inner.RemoveMemberAsync(member, ct);
        public Task ReplaceMembersAsync(IEnumerable<CouncilMember> existingMembers, IEnumerable<CouncilMember> newMembers, CancellationToken ct = default)
            => inner.ReplaceMembersAsync(existingMembers, newMembers, ct);
        public Task<IReadOnlySet<Guid>> GetUserIdsWithAnyCouncilMembershipAsync(IEnumerable<Guid> userIds, Guid? excludeCouncilId = null, CancellationToken ct = default)
            => inner.GetUserIdsWithAnyCouncilMembershipAsync(userIds, excludeCouncilId, ct);
        public Task AddAssignmentsAsync(IEnumerable<CouncilAssignment> assignments, CancellationToken ct = default) => inner.AddAssignmentsAsync(assignments, ct);
        public Task ReorderAssignmentsAsync(IReadOnlyList<CouncilAssignment> assignments, IReadOnlyDictionary<Guid, int> orders, CancellationToken ct = default)
            => inner.ReorderAssignmentsAsync(assignments, orders, ct);
        public Task RemoveAssignmentAsync(CouncilAssignment assignment, CancellationToken ct = default)
            => inner.RemoveAssignmentAsync(assignment, ct);
        public Task<CouncilAssignment?> GetAssignmentWithCouncilAndMembersAsync(Guid assignmentId, CancellationToken ct = default)
            => inner.GetAssignmentWithCouncilAndMembersAsync(assignmentId, ct);
        public Task<IReadOnlyList<GradingCriteria>> GetGradingCriteriaByBatchIdAsync(Guid batchId, CancellationToken ct = default)
            => inner.GetGradingCriteriaByBatchIdAsync(batchId, ct);
        public Task<IReadOnlyList<CouncilScore>> GetCouncilScoresWithDetailsByAssignmentIdAsync(Guid assignmentId, CancellationToken ct = default)
        {
            CouncilScoresQueryCount++;
            return inner.GetCouncilScoresWithDetailsByAssignmentIdAsync(assignmentId, ct);
        }
        public Task<IReadOnlyList<EvaluationDetail>> GetEvaluationDetailsByEvaluationIdAsync(Guid evaluationId, CancellationToken ct = default)
            => inner.GetEvaluationDetailsByEvaluationIdAsync(evaluationId, ct);
        public Task<CouncilScore?> GetCouncilScoreAsync(Guid assignmentId, Guid councilMemberId, CancellationToken ct = default)
            => inner.GetCouncilScoreAsync(assignmentId, councilMemberId, ct);
        public Task SaveCouncilScoreAsync(CouncilScore score, IEnumerable<EvaluationDetail> details, CancellationToken ct = default)
            => inner.SaveCouncilScoreAsync(score, details, ct);
        public Task<ReviewerAssignment?> GetActiveReviewerAssignmentByGroupIdAsync(Guid groupId, CancellationToken ct = default)
            => inner.GetActiveReviewerAssignmentByGroupIdAsync(groupId, ct);
        public Task<AdvisorEvaluation?> GetAdvisorEvaluationByGroupIdAsync(Guid groupId, CancellationToken ct = default)
            => inner.GetAdvisorEvaluationByGroupIdAsync(groupId, ct);
        public Task<ScoreSummary?> GetScoreSummaryByGroupIdAsync(Guid groupId, CancellationToken ct = default)
            => inner.GetScoreSummaryByGroupIdAsync(groupId, ct);
        public Task SaveScoreSummaryAsync(ScoreSummary summary, IEnumerable<ScoreSummaryComponent> components, CancellationToken ct = default)
            => inner.SaveScoreSummaryAsync(summary, components, ct);
        public Task UpdateMinuteLockStatusAsync(Guid assignmentId, CouncilMinuteLockStatus status, CancellationToken ct = default)
            => inner.UpdateMinuteLockStatusAsync(assignmentId, status, ct);
        public Task<CouncilMinute?> GetMinuteByAssignmentIdAsync(Guid assignmentId, CancellationToken ct = default)
            => inner.GetMinuteByAssignmentIdAsync(assignmentId, ct);
        public Task SaveMinuteAsync(CouncilMinute minute, CancellationToken ct = default) => inner.SaveMinuteAsync(minute, ct);
        public Task SaveCouncilMinuteDraftAsync(Guid assignmentId, string? studentFeedback, string? overrideMarker, CancellationToken ct = default)
            => inner.SaveCouncilMinuteDraftAsync(assignmentId, studentFeedback, overrideMarker, ct);
    }

    private sealed class FakeStudentSearchReader : IStudentSearchReader
    {
        public Task<IReadOnlyList<Guid>> FindIdsByNameOrCodeAsync(string keyword, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class EmptyScoreSummaryStore : Application.ScoreSummaries.IScoreSummaryStore
    {
        public Task<IReadOnlyDictionary<Guid, Application.ScoreSummaries.ScoreFacts>> FactsAsync(IReadOnlyCollection<Guid> groups, bool locked, CancellationToken token)
            => Task.FromResult<IReadOnlyDictionary<Guid, Application.ScoreSummaries.ScoreFacts>>(
                groups.ToDictionary(g => g, _ => new Application.ScoreSummaries.ScoreFacts([], [], [], [], [], [], [], [], [])));
        public Task<IReadOnlyDictionary<Guid, Application.ScoreSummaries.ScoreHistory>> HistoriesAsync(IReadOnlyCollection<Guid> groups, CancellationToken token)
            => Task.FromResult<IReadOnlyDictionary<Guid, Application.ScoreSummaries.ScoreHistory>>(new Dictionary<Guid, Application.ScoreSummaries.ScoreHistory>());
        public Task KeyLockAsync(Guid actor, string operation, Guid key, CancellationToken token) => Task.CompletedTask;
        public Task<ScoreSummaryIdempotency?> ReceiptAsync(Guid actor, string operation, Guid key, CancellationToken token) => Task.FromResult<ScoreSummaryIdempotency?>(null);
        public Task LockAdvisorsAsync(IReadOnlyCollection<Guid> groups, CancellationToken token) => Task.CompletedTask;
        public Task<ScoreSummary?> SummaryAsync(Guid group, bool locked, CancellationToken token) => Task.FromResult<ScoreSummary?>(null);
        public Task<Guid?> AssignmentGroupAsync(Guid assignment, CancellationToken token) => Task.FromResult<Guid?>(null);
        public Task<IReadOnlyList<Guid>> CouncilGroupsAsync(Guid council, CancellationToken token) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<bool> PublishedAsync(IReadOnlyCollection<Guid> groups, CancellationToken token) => Task.FromResult(false);
        public void Add(ScoreSummary summary) { }
        public void Add(ScoreSummaryRevision revision, IReadOnlyList<ScoreSummaryComponent> components) { }
        public void Add(ScoreSummaryIdempotency receipt) { }
    }

    private sealed class EmptyArchiveProofContract : IFinalSubmissionArchiveProofContract
    {
        public Task<FinalSubmissionArchiveProofContractDto> GetArchiveProofAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.Missing, groupId, Guid.Empty, null, null, null, null, null, null, null, null, null, null, null, []));
        public Task<IReadOnlyList<FinalSubmissionArchiveProofContractDto>> GetManyArchiveProofsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FinalSubmissionArchiveProofContractDto>>(
                groupIds.Select(id => new FinalSubmissionArchiveProofContractDto(ArchiveProofOutcome.Missing, id, Guid.Empty, null, null, null, null, null, null, null, null, null, null, null, [])).ToList());
        public Task<FinalSubmissionArchiveProofContractDto> LockArchiveProofForCalculationAsync(Guid groupId, CancellationToken cancellationToken = default)
            => GetArchiveProofAsync(groupId, cancellationToken);
    }

    #endregion
}
