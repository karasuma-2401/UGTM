using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.AcademicPeriods.Infrastructure.Auditing;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Infrastructure.Auditing;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Infrastructure.Auditing;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Architecture.Tests;

public sealed class CrossModuleAuditOutboxTests
{
    private static readonly DateTime Now =
        new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);

    private static readonly Guid ActorUserId = Guid.NewGuid();
    private static readonly Guid ActorSessionId = Guid.NewGuid();

    [Fact]
    public void AcademicPeriods_outbox_captures_audit_context_in_its_own_context()
    {
        var options = new DbContextOptionsBuilder<AcademicPeriodsDbContext>()
            .UseNpgsql("Host=localhost;Database=audit_outbox_test;Username=test")
            .Options;
        using var db = new AcademicPeriodsDbContext(options);
        var outbox = new AcademicPeriodsAuditOutbox(db, CurrentUser(), HttpAccessor());

        outbox.Add(Entry(IntegrationAuditActions.BatchConfigurationUpdated), Now);

        var message = Assert.Single(db.AuditOutboxMessages.Local);
        AssertEnvelope(message.Id, message.Action, message.PayloadJson);
        Assert.Equal(EntityState.Added, db.Entry(message).State);
    }

    [Fact]
    public void DefenseCouncil_outbox_captures_audit_context_in_its_own_context()
    {
        var options = new DbContextOptionsBuilder<DefenseCouncilDbContext>()
            .UseNpgsql("Host=localhost;Database=audit_outbox_test;Username=test")
            .Options;
        using var db = new DefenseCouncilDbContext(options);
        var outbox = new DefenseCouncilAuditOutbox(db, CurrentUser(), HttpAccessor());

        outbox.Add(Entry(IntegrationAuditActions.CouncilMinuteUnlockGranted), Now);

        var message = Assert.Single(db.AuditOutboxMessages.Local);
        AssertEnvelope(message.Id, message.Action, message.PayloadJson);
        Assert.Equal(EntityState.Added, db.Entry(message).State);
    }

    [Fact]
    public void ThesisManagement_outbox_captures_audit_context_in_its_own_context()
    {
        var options = new DbContextOptionsBuilder<ThesisManagementDbContext>()
            .UseNpgsql("Host=localhost;Database=audit_outbox_test;Username=test")
            .Options;
        using var db = new ThesisManagementDbContext(options);
        var outbox = new ThesisManagementAuditOutbox(db, CurrentUser(), HttpAccessor());

        outbox.Add(Entry(IntegrationAuditActions.RenameRequestApproved), Now);

        var message = Assert.Single(db.AuditOutboxMessages.Local);
        AssertEnvelope(message.Id, message.Action, message.PayloadJson);
        Assert.Equal(EntityState.Added, db.Entry(message).State);
    }

    private static AuditIntegrationEntry Entry(string action) => new(
        "TestTarget",
        "target-1",
        action,
        "Success",
        null,
        new { ChangedFields = new[] { "status" } });

    private static void AssertEnvelope(Guid id, string action, string payloadJson)
    {
        var integrationEvent = JsonSerializer.Deserialize<AuditIntegrationEvent>(payloadJson);
        Assert.NotNull(integrationEvent);
        Assert.Equal(id, integrationEvent.EventId);
        Assert.Equal(action, integrationEvent.Action);
        Assert.Equal(ActorUserId, integrationEvent.ActorUserId);
        Assert.Equal(ActorSessionId, integrationEvent.ActorSessionId);
        Assert.Equal("audit-request-123", integrationEvent.RequestId);
        Assert.Equal(Now, integrationEvent.OccurredAtUtc);
        Assert.Contains("ChangedFields", integrationEvent.MetadataJson);
    }

    private static ICurrentUser CurrentUser()
        => new FakeCurrentUser(ActorUserId, ActorSessionId);

    private static IHttpContextAccessor HttpAccessor()
        => new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "audit-request-123"
            }
        };

    private sealed class FakeCurrentUser(Guid? userId, Guid? sessionId) : ICurrentUser
    {
        public bool IsAuthenticated => userId.HasValue;
        public Guid? UserId => userId;
        public Guid? SessionId => sessionId;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }
}
