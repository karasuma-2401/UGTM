using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Tests.Integration;

public sealed partial class ReviewerAssignmentTransactionIntegrationTests
{
    [Fact]
    public async Task Upload_cleanup_keeps_committed_file_even_if_caller_loses_response()
    {
        var storage = new CleanupStorage();
        await using var provider = UploadProvider(storage);
        await using var scope = provider.CreateAsyncScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IThesisUploadLifecycle>();
        var db = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        const string key = "tests/committed.pdf";
        var id = await lifecycle.BeginAsync(key);
        storage.Objects.Add(key);
        db.SubmissionFiles.Add(NewUploadFile(key));
        var result = await lifecycle.SaveAsync(id, async token => { await db.SaveChangesAsync(token); return null; }, Error.Conflict("expired", "expired"));
        Assert.Null(result);
        // The response could be lost at this point; cleanup must consult the committed row.
        await lifecycle.FinishAsync(id);
        Assert.Contains(key, storage.Objects);
        await using var verify = Db<ThesisManagementDbContext>();
        Assert.Equal("Committed", (await verify.ThesisUploadAttempts.SingleAsync(a => a.Id == id)).State);
        Assert.True(await verify.SubmissionFiles.AnyAsync(f => f.ObjectKey == key));
    }

    [Fact]
    public async Task Upload_cleanup_retries_failed_delete_after_rolled_back_save()
    {
        var storage = new CleanupStorage { FailNextDelete = true };
        await using var provider = UploadProvider(storage);
        await using var scope = provider.CreateAsyncScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IThesisUploadLifecycle>();
        var db = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        const string key = "tests/rollback.pdf";
        var id = await lifecycle.BeginAsync(key);
        storage.Objects.Add(key);
        db.SubmissionFiles.Add(NewUploadFile(key));
        await Assert.ThrowsAsync<IOException>(() => lifecycle.SaveAsync(id, async token =>
        {
            await db.SaveChangesAsync(token);
            throw new IOException("failure after save before commit");
        }, Error.Conflict("expired", "expired")));
        await lifecycle.FinishAsync(id);
        Assert.Contains(key, storage.Objects);
        await using (var verify = Db<ThesisManagementDbContext>())
        {
            Assert.Equal("CleanupPending", (await verify.ThesisUploadAttempts.SingleAsync(a => a.Id == id)).State);
            Assert.False(await verify.SubmissionFiles.AnyAsync(f => f.ObjectKey == key));
        }
        await lifecycle.FinishAsync(id);
        Assert.DoesNotContain(key, storage.Objects);
        await using var final = Db<ThesisManagementDbContext>();
        Assert.Equal("Cleaned", (await final.ThesisUploadAttempts.SingleAsync(a => a.Id == id)).State);
    }

    [Fact]
    public async Task Expired_upload_cannot_commit_and_late_upload_is_cleaned_again()
    {
        var storage = new CleanupStorage();
        var clock = new RegistrationClock(Now);
        await using var provider = UploadProvider(storage, clock);
        await using var scope = provider.CreateAsyncScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IThesisUploadLifecycle>();
        const string key = "tests/late.pdf";
        var id = await lifecycle.BeginAsync(key);
        storage.Objects.Add(key);
        clock.Advance(TimeSpan.FromMinutes(11));
        await lifecycle.FinishAsync(id);
        storage.Objects.Add(key); // The storage upload completed after the first cleanup.
        var error = await lifecycle.SaveAsync(id, _ => throw new InvalidOperationException("must not save"), Error.Conflict("expired", "expired"));
        Assert.Equal("expired", error!.Code);
        await lifecycle.FinishAsync(id);
        Assert.DoesNotContain(key, storage.Objects);
    }

    [Fact]
    public async Task Cleanup_waits_for_in_flight_commit_and_never_deletes_committed_object()
    {
        var storage = new CleanupStorage();
        await using var provider = UploadProvider(storage);
        await using var scope = provider.CreateAsyncScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IThesisUploadLifecycle>();
        var db = scope.ServiceProvider.GetRequiredService<ThesisManagementDbContext>();
        const string key = "tests/in-flight.pdf";
        var id = await lifecycle.BeginAsync(key);
        storage.Objects.Add(key);
        db.SubmissionFiles.Add(NewUploadFile(key));
        var saved = Signal<bool>();
        var release = Signal<bool>();
        var writing = lifecycle.SaveAsync(id, async token =>
        {
            await db.SaveChangesAsync(token);
            saved.SetResult(true);
            await release.Task.WaitAsync(token);
            return null;
        }, Error.Conflict("expired", "expired"));
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var cleanup = lifecycle.FinishAsync(id);
        try
        {
            Assert.NotSame(cleanup, await Task.WhenAny(cleanup, Task.Delay(250)));
        }
        finally { release.SetResult(true); }
        Assert.Null(await writing.WaitAsync(TimeSpan.FromSeconds(10)));
        await cleanup.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains(key, storage.Objects);
    }

    [Fact]
    public async Task Background_cleanup_recovers_abandoned_upload_and_rechecks_for_late_object()
    {
        var storage = new CleanupStorage();
        var clock = new RegistrationClock(Now);
        await using var provider = UploadProvider(storage, clock);
        const string key = "tests/abandoned.pdf";
        Guid attemptId;
        await using (var request = provider.CreateAsyncScope())
        {
            attemptId = await request.ServiceProvider.GetRequiredService<IThesisUploadLifecycle>().BeginAsync(key);
            storage.Objects.Add(key);
        } // Simulate a process disappearing before it can call FinishAsync.

        clock.Advance(TimeSpan.FromMinutes(11));
        await using (var worker = provider.CreateAsyncScope())
            await worker.ServiceProvider.GetRequiredService<IThesisUploadCleanup>().CleanExpiredAsync();
        Assert.DoesNotContain(key, storage.Objects);

        storage.Objects.Add(key); // Object storage completed the old request after the delete.
        clock.Advance(TimeSpan.FromMinutes(16));
        await using (var worker = provider.CreateAsyncScope())
            await worker.ServiceProvider.GetRequiredService<IThesisUploadCleanup>().CleanExpiredAsync();
        Assert.DoesNotContain(key, storage.Objects);
        await using var verify = Db<ThesisManagementDbContext>();
        Assert.Equal("Cleaned", (await verify.ThesisUploadAttempts.SingleAsync(a => a.Id == attemptId)).State);
    }

    private SubmissionFile NewUploadFile(string key) => new()
    {
        Id = Guid.NewGuid(),
        EntityType = SubmissionEntityType.Syllabus,
        EntityId = Guid.NewGuid(),
        VersionNo = 1,
        FileName = "file.pdf",
        ObjectKey = key,
        ContentType = "application/pdf",
        FileSize = 1,
        FileExtension = ".pdf",
        UploadedBy = _actor,
        UploadedAt = Now
    };

    private ServiceProvider UploadProvider(CleanupStorage storage, TimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CONNECTION_STRING"] = fixture.ConnectionString,
            ["ThesisManagement:Email:PublicWebBaseUrl"] = "https://workflow.test"
        }).Build();
        new ThesisManagementModule().RegisterModule(services, configuration);
        services.RemoveAll<IThesisSubmissionObjectStorage>();
        services.AddSingleton<IThesisSubmissionObjectStorage>(storage);
        services.RemoveAll<TimeProvider>();
        services.AddSingleton(clock ?? TimeProvider.System);
        return services.BuildServiceProvider();
    }

    private sealed class CleanupStorage : IThesisSubmissionObjectStorage
    {
        public HashSet<string> Objects { get; } = [];
        public bool FailNextDelete { get; set; }
        public Task UploadAsync(ThesisSubmissionObjectWriteRequest request, Stream content, CancellationToken cancellationToken = default)
        { Objects.Add(request.ObjectKey); return Task.CompletedTask; }
        public Task<Uri> CreateDownloadUriAsync(string objectKey, TimeSpan validFor, CancellationToken cancellationToken = default)
            => Task.FromResult(new Uri("https://storage.test/"));
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            if (FailNextDelete) { FailNextDelete = false; throw new IOException("delete failed"); }
            Objects.Remove(objectKey);
            return Task.CompletedTask;
        }
    }
}
