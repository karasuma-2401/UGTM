using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class AuditLogWriterTests
{
    private static readonly DateTime Now =
        new(2026, 10, 17, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Add_captures_request_and_actor_context_without_saving()
    {
        using var db = CreateDbContext();
        var actorUserId = Guid.NewGuid();
        var actorSessionId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "request-123"
        };
        var writer = new AuditLogWriter(
            db,
            new FakeCurrentUser(actorUserId, actorSessionId),
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeClock(Now));

        writer.Add(new AuditEntry(
            "User",
            Guid.NewGuid().ToString("D"),
            AuditActions.UserCreated,
            AuditOutcomes.Success,
            null,
            new { Roles = new[] { "Lecturer" } }));

        var auditLog = Assert.Single(db.AuditLogs.Local);
        Assert.Equal(actorUserId, auditLog.ActorUserId);
        Assert.Equal(actorSessionId, auditLog.ActorSessionId);
        Assert.Equal("request-123", auditLog.RequestId);
        Assert.Equal(Now, auditLog.Timestamp);
        Assert.Equal(EntityState.Added, db.Entry(auditLog).State);

        using var metadata = JsonDocument.Parse(auditLog.MetadataJson!);
        Assert.Equal("Lecturer", metadata.RootElement.GetProperty("Roles")[0].GetString());
    }

    [Fact]
    public void Add_generates_request_id_without_http_context_and_keeps_null_metadata()
    {
        using var db = CreateDbContext();
        var writer = new AuditLogWriter(
            db,
            new FakeCurrentUser(null, null),
            new HttpContextAccessor(),
            new FakeClock(Now));

        writer.Add(new AuditEntry(
            "System",
            "cleanup",
            "Identity.System.Cleanup",
            AuditOutcomes.Success,
            null,
            null));

        var auditLog = Assert.Single(db.AuditLogs.Local);
        Assert.False(string.IsNullOrWhiteSpace(auditLog.RequestId));
        Assert.Null(auditLog.MetadataJson);
    }

    [Fact]
    public void AuditLog_rejects_non_utc_timestamp()
    {
        using var db = CreateDbContext();
        var writer = new AuditLogWriter(
            db,
            new FakeCurrentUser(null, null),
            new HttpContextAccessor(),
            new FakeClock(DateTime.SpecifyKind(Now, DateTimeKind.Local)));

        Assert.Throws<ArgumentException>(() => writer.Add(new AuditEntry(
            "User",
            "1",
            AuditActions.UserUpdated,
            AuditOutcomes.Success,
            null,
            null)));
    }

    private static IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=audit_log_unit_test;Username=test;Password=test")
            .Options;
        return new IdentityDbContext(options);
    }

    private sealed class FakeCurrentUser(Guid? userId, Guid? sessionId) : ICurrentUser
    {
        public bool IsAuthenticated => userId.HasValue;
        public Guid? UserId => userId;
        public Guid? SessionId => sessionId;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class FakeClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}
