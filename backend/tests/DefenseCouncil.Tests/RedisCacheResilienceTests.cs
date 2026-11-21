using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Infrastructure.Caching;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMinuteWorkspace;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class RedisCacheResilienceTests
{
    [Fact]
    public async Task Concurrent_cache_misses_share_one_fill_across_request_scopes()
    {
        var store = Store();
        var availability = new CacheAvailability();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async Task<string> Load(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            await release.Task.WaitAsync(token);
            return "fresh-score";
        }
        var requests = Enumerable.Range(0, 12)
            .Select(_ => new DistributedCacheService(store, availability: availability)
                .GetOrCreateAsync("results", Load, TimeSpan.FromMinutes(5))).ToArray();
        Assert.Equal(1, calls);
        release.SetResult();
        Assert.All(await Task.WhenAll(requests), value => Assert.Equal("fresh-score", value));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Failed_fill_releases_the_gate_for_a_later_request()
    {
        var service = new DistributedCacheService(Store());
        await Assert.ThrowsAsync<IOException>(() => service.GetOrCreateAsync<string>("results",
            _ => throw new IOException("Database unavailable"), TimeSpan.FromMinutes(5)));
        Assert.Equal("recovered", await service.GetOrCreateAsync("results",
            _ => Task.FromResult("recovered"), TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task Workspace_captures_revision_before_loading_a_minute_snapshot()
    {
        var repository = new FakeCouncilRepository();
        var council = Council.Create(Guid.NewGuid(), "HD-RACE", new DateOnly(2026, 12, 20), "08:00 - 11:30", "A101");
        repository.Councils.Add(council);
        var assignment = new CouncilAssignment { Id = Guid.NewGuid(), CouncilId = council.Id, GroupId = Guid.NewGuid(), OrderNumber = 1 };
        repository.Assignments.Add(assignment);
        repository.Minutes.Add(new CouncilMinute { Id = Guid.NewGuid(), CouncilAssignmentId = assignment.Id });
        var revisions = new CallbackRevision(() =>
        {
            repository.Minutes.Clear();
            repository.Minutes.Add(new CouncilMinute { Id = Guid.NewGuid(), CouncilAssignmentId = assignment.Id, LockStatus = CouncilMinuteLockStatus.Locked });
        });
        var service = new DistributedCacheService(Store(), revisions: revisions);
        var loader = new CouncilMinuteWorkspaceLoader(repository, new FakeCouncilUserInfoReader(),
            new FakeDefenseGroupContract(), new FakeTopicDefenseContract(), new FakeMinuteConfigurationReader());
        var user = new FakeCurrentUser { UserId = Guid.NewGuid(), Roles = new HashSet<string> { "Faculty" } };
        var handler = new GetCouncilMinuteWorkspaceHandler(repository, loader, user, service);

        var result = await handler.HandleAsync(new GetCouncilMinuteWorkspaceQuery(assignment.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("Locked", result.Value.AssignmentInfo.LockStatus);
    }

    [Fact]
    public async Task Missing_version_key_never_resurrects_an_older_payload()
    {
        var store = Store();
        var service = new DistributedCacheService(store);
        var original = await service.GetVersionAsync("batch");
        await service.SetAsync($"result:{original}", "old-score");
        await service.BumpVersionAsync("batch");
        await store.RemoveAsync(DistributedCacheService.DataKeyPrefix + "ver:batch");

        var afterEviction = await service.GetVersionAsync("batch");
        Assert.NotEqual(original, afterEviction);
        Assert.Null(await service.GetAsync<string>($"result:{afterEviction}"));
    }

    [Fact]
    public async Task Failed_invalidation_bypasses_stale_entries_across_local_request_scopes()
    {
        var store = new ControlledCache(Store());
        var clock = new TestTimeProvider();
        var availability = new CacheAvailability(clock);
        var reader = new DistributedCacheService(store, availability: availability);
        var version = await reader.GetVersionAsync("batch");
        await reader.SetAsync($"score:{version}", "before-save");
        store.FailWrites = true;
        var writer = new DistributedCacheService(store, availability: availability);
        await writer.BumpVersionAsync("batch");
        store.FailWrites = false;
        clock.Advance(TimeSpan.FromSeconds(10));

        var nextRequest = new DistributedCacheService(store, availability: availability);
        Assert.Null(await nextRequest.GetAsync<string>($"score:{version}"));
        Assert.NotEqual(version, await nextRequest.GetVersionAsync("batch"));
    }

    [Fact]
    public async Task Committed_revision_invalidates_other_instances_without_any_redis_version_write()
    {
        var store = new ControlledCache(Store());
        var database = new RevisionDatabase();
        var before = new DistributedCacheService(store, revisions: new RevisionSnapshot(database));
        var oldVersion = await before.GetVersionAsync("global");
        await before.SetAsync($"score:{oldVersion}", "old-score");

        // The transaction commits while Redis writes are unavailable, then the writer exits.
        store.FailWrites = true;
        database.Commit();
        var writer = new DistributedCacheService(store, revisions: new RevisionSnapshot(database));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await writer.BumpVersionAsync("global", cancelled.Token);
        store.FailWrites = false;

        var otherInstance = new DistributedCacheService(store, revisions: new RevisionSnapshot(database));
        var currentVersion = await otherInstance.GetVersionAsync("global");
        Assert.NotEqual(oldVersion, currentVersion);
        Assert.Null(await otherInstance.GetAsync<string>($"score:{currentVersion}"));
        // An already-running reader can finish against its captured revision.
        Assert.Equal(oldVersion, await before.GetVersionAsync("global"));
    }

    [Fact]
    public async Task Revision_failure_bypasses_payload_reads_and_writes_for_the_request()
    {
        var store = new ControlledCache(Store());
        await new DistributedCacheService(store).SetAsync("score", "old");
        var service = new DistributedCacheService(store, revisions: new FailedRevision());
        await service.GetVersionAsync("global");
        await service.SetAsync("score", "replacement");

        Assert.Null(await service.GetAsync<string>("score"));
        Assert.Equal("old", await new DistributedCacheService(store).GetAsync<string>("score"));
    }

    [Fact]
    public async Task Outage_backoff_skips_repeated_operations_and_recovers()
    {
        var store = new ControlledCache(Store()) { FailReads = true };
        var clock = new TestTimeProvider();
        var availability = new CacheAvailability(clock);
        var first = new DistributedCacheService(store, availability: availability);
        Assert.Null(await first.GetAsync<string>("score"));
        var second = new DistributedCacheService(store, availability: availability);
        Assert.Null(await second.GetAsync<string>("score"));
        Assert.Equal(1, store.ReadCount);

        clock.Advance(TimeSpan.FromSeconds(6));
        store.FailReads = false;
        await second.SetAsync("score", "recovered");
        Assert.Equal("recovered", await second.GetAsync<string>("score"));
        Assert.Equal(2, store.ReadCount);
    }

    [Fact]
    public async Task Payload_ttl_is_bounded_and_jitter_never_extends_it()
    {
        var store = new ControlledCache(Store());
        var service = new DistributedCacheService(store);
        await service.SetAsync("score", "value", TimeSpan.FromDays(2));
        Assert.InRange(store.LastTtl!.Value, TimeSpan.FromMinutes(27), TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Global_score_invalidation_changes_the_rubric_cache_key()
    {
        var service = new DistributedCacheService(Store());
        var batch = Guid.NewGuid();
        async Task<string> Key() => DefenseCouncilCacheKeys.GradingRubric(batch,
            await service.GetVersionAsync(DefenseCouncilCacheKeys.BatchScope(batch)),
            await service.GetVersionAsync(DefenseCouncilCacheKeys.GlobalScope));
        var before = await Key();
        await service.SetAsync(before, "unlocked");
        await DefenseCouncilCacheKeys.InvalidateGlobalAsync(service);
        var after = await Key();
        Assert.NotEqual(before, after);
        Assert.Null(await service.GetAsync<string>(after));
    }

    private static MemoryDistributedCache Store() => new(Options.Create(new MemoryDistributedCacheOptions()));

    private sealed class RevisionDatabase
    {
        public string Revision { get; private set; } = Guid.NewGuid().ToString("N");
        public void Commit() => Revision = Guid.NewGuid().ToString("N");
    }

    private sealed class RevisionSnapshot(RevisionDatabase database) : ICacheRevisionProvider
    {
        private string? _snapshot;
        public Task<string> GetAsync(CancellationToken cancellationToken) => Task.FromResult(_snapshot ??= database.Revision);
        public void Reset() => _snapshot = null;
    }

    private sealed class FailedRevision : ICacheRevisionProvider
    {
        public Task<string> GetAsync(CancellationToken cancellationToken) => throw new IOException("Revision unavailable");
        public void Reset() { }
    }

    private sealed class CallbackRevision(Action beforeCapture) : ICacheRevisionProvider
    {
        private bool _captured;
        public Task<string> GetAsync(CancellationToken cancellationToken)
        {
            if (!_captured) { beforeCapture(); _captured = true; }
            return Task.FromResult("after-commit");
        }
        public void Reset() => _captured = false;
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class ControlledCache(IDistributedCache inner) : IDistributedCache
    {
        public bool FailWrites { get; set; }
        public bool FailReads { get; set; }
        public int ReadCount { get; private set; }
        public TimeSpan? LastTtl { get; private set; }
        public byte[]? Get(string key) => throw new NotSupportedException();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            ReadCount++;
            if (FailReads) throw new IOException("Redis read unavailable");
            return inner.GetAsync(key, token);
        }
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            if (FailWrites) throw new IOException("Redis write unavailable");
            LastTtl = options.AbsoluteExpirationRelativeToNow;
            return inner.SetAsync(key, value, options, token);
        }
        public void Refresh(string key) => inner.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
        public void Remove(string key) => inner.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);
    }
}
