using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UGTM.Api.Modules;
using UGTM.Modules.AcademicPeriods.Application.Notifications;
using UGTM.Modules.AcademicPeriods.Infrastructure.Notifications;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Infrastructure.Notifications;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.Notifications.Infrastructure.Relay;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Infrastructure.Notifications;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Application.Notifications;
using UGTM.Modules.TopicCatalog.Infrastructure.Notifications;
using UGTM.Modules.TopicCatalog.Infrastructure.Persistence;

namespace UGTM.Architecture.Tests;

public sealed class NotificationPhase4RelayTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 25, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AcademicPeriods_outbox_stages_notification_event_in_its_own_context()
    {
        var options = new DbContextOptionsBuilder<AcademicPeriodsDbContext>()
            .UseNpgsql("Host=localhost;Database=notification_outbox_test;Username=test")
            .Options;
        using var db = new AcademicPeriodsDbContext(options);
        var outbox = new AcademicPeriodsNotificationOutbox(db);

        outbox.Enqueue(SampleEntry("BATCH_OPENED"), NowUtc);

        var message = Assert.Single(db.NotificationEventOutboxMessages.Local);
        Assert.Equal(EntityState.Added, db.Entry(message).State);
        AssertEnvelope(message.EventId, "AcademicPeriods", "BATCH_OPENED", message.PayloadJson);
    }

    [Fact]
    public void TopicCatalog_outbox_stages_notification_event_in_its_own_context()
    {
        var options = new DbContextOptionsBuilder<TopicCatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=notification_outbox_test;Username=test")
            .Options;
        using var db = new TopicCatalogDbContext(options);
        var outbox = new TopicCatalogNotificationOutbox(db);

        outbox.Enqueue(SampleEntry("TOPIC_REVISION_REQUESTED"), NowUtc);

        var message = Assert.Single(db.NotificationEventOutboxMessages.Local);
        Assert.Equal(EntityState.Added, db.Entry(message).State);
        AssertEnvelope(message.EventId, "TopicCatalog", "TOPIC_REVISION_REQUESTED", message.PayloadJson);
    }

    [Fact]
    public void ThesisManagement_outbox_stages_notification_event_in_its_own_context()
    {
        var options = new DbContextOptionsBuilder<ThesisManagementDbContext>()
            .UseNpgsql("Host=localhost;Database=notification_outbox_test;Username=test")
            .Options;
        using var db = new ThesisManagementDbContext(options);
        var outbox = new ThesisNotificationOutbox(db);

        outbox.Enqueue(SampleEntry("SUBMISSION_CONFIRMED"), NowUtc);

        var message = Assert.Single(db.NotificationEventOutboxMessages.Local);
        Assert.Equal(EntityState.Added, db.Entry(message).State);
        AssertEnvelope(message.EventId, "ThesisManagement", "SUBMISSION_CONFIRMED", message.PayloadJson);
    }

    [Fact]
    public void DefenseCouncil_outbox_stages_notification_event_in_its_own_context()
    {
        var options = new DbContextOptionsBuilder<DefenseCouncilDbContext>()
            .UseNpgsql("Host=localhost;Database=notification_outbox_test;Username=test")
            .Options;
        using var db = new DefenseCouncilDbContext(options);
        var outbox = new DefenseCouncilNotificationOutbox(db);

        outbox.Enqueue(SampleEntry("COUNCIL_MEMBER_ASSIGNED"), NowUtc);

        var message = Assert.Single(db.NotificationEventOutboxMessages.Local);
        Assert.Equal(EntityState.Added, db.Entry(message).State);
        AssertEnvelope(message.EventId, "DefenseCouncil", "COUNCIL_MEMBER_ASSIGNED", message.PayloadJson);
    }

    [Fact]
    public async Task RelayConsumer_resolves_recipients_deduplicates_excludes_actors_and_forwards_rich_payload()
    {
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();
        var actorAdvisor = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var directory = new FakeRecipientDirectory([
            new NotificationRecipientContractDto(studentA, "Nguyễn Văn A", "a@gm.uit.edu.vn"),
            new NotificationRecipientContractDto(studentA, "Nguyễn Văn A", "a@gm.uit.edu.vn"),
            new NotificationRecipientContractDto(studentB, "Trần Thị B", "b@gm.uit.edu.vn"),
            new NotificationRecipientContractDto(actorAdvisor, "TS. Lê Văn C", "c@uit.edu.vn")
        ]);
        var ingestion = new CapturingIngestionService();
        var consumer = new NotificationEventRelayConsumer(
            directory,
            ingestion,
            NullLogger<NotificationEventRelayConsumer>.Instance);

        var entry = new NotificationOutboxEntry(
            NotificationType: "SUBMISSION_CONFIRMED",
            Title: "Đã ghi nhận nộp Đề cương KLTN",
            Content: "Hệ thống đã ghi nhận tệp nộp đề cương phiên bản v2 của nhóm.",
            RecipientUserIds: [studentA, studentB],
            RecipientRoles: ["Lecturer"],
            RecipientPermissions: ["topics.review"],
            ExcludeUserIds: [actorAdvisor],
            ResourceType: "Syllabus",
            ResourceId: resourceId,
            AcademicBatchId: batchId,
            ActionPath: "/student/submissions",
            Priority: "Critical",
            EnqueuePush: true,
            EnqueueEmail: true,
            EmailSubject: "[UGTM] Xác nhận nộp Đề cương KLTN",
            EmailCategoryLabel: "Cổng thẩm định 1",
            EmailCtaLabel: "Xem lịch sử nộp bài",
            EmailNotice: "Vui lòng theo dõi phản hồi của GVHD trên hệ thống.",
            EmailMetadata:
            [
                new NotificationEmailMetaItem("Biểu mẫu", "Đề cương KLTN"),
                new NotificationEmailMetaItem("Phiên bản", "v2")
            ]);

        var integrationEvent = new NotificationIntegrationEvent(
            eventId,
            "ThesisManagement",
            "SUBMISSION_CONFIRMED",
            PayloadVersion: 1,
            entry,
            NowUtc);

        await consumer.ConsumeAsync(integrationEvent);

        Assert.Equal([studentA, studentB], directory.LastUserIds);
        Assert.Equal(["Lecturer"], directory.LastRoleNames);
        Assert.Equal(["topics.review"], directory.LastPermissionCodes);

        Assert.Equal(2, ingestion.CapturedRequests.Count);
        var reqA = Assert.Single(ingestion.CapturedRequests, r => r.RecipientId == studentA);
        var reqB = Assert.Single(ingestion.CapturedRequests, r => r.RecipientId == studentB);

        Assert.Equal("a@gm.uit.edu.vn", reqA.RecipientEmail);
        Assert.Equal("Nguyễn Văn A", reqA.RecipientFullName);
        Assert.Equal("ThesisManagement", reqA.SourceModule);
        Assert.Equal(eventId, reqA.SourceEventId);
        Assert.Equal($"evt:{eventId:N}", reqA.DeduplicationKey);
        Assert.Equal("Critical", reqA.Priority);
        Assert.True(reqA.EnqueueEmail);
        Assert.Equal("[UGTM] Xác nhận nộp Đề cương KLTN", reqA.EmailSubject);
        Assert.Equal("Cổng thẩm định 1", reqA.EmailCategoryLabel);
        Assert.Equal("Xem lịch sử nộp bài", reqA.EmailCtaLabel);
        Assert.Equal(2, reqA.EmailMetadata?.Count);

        Assert.Equal("b@gm.uit.edu.vn", reqB.RecipientEmail);
        Assert.Equal("Trần Thị B", reqB.RecipientFullName);
    }

    [Fact]
    public async Task RelayConsumer_skips_ingestion_when_all_resolved_recipients_are_excluded()
    {
        var onlyActor = Guid.NewGuid();
        var directory = new FakeRecipientDirectory([
            new NotificationRecipientContractDto(onlyActor, "Actor User", "actor@uit.edu.vn")
        ]);
        var ingestion = new CapturingIngestionService();
        var consumer = new NotificationEventRelayConsumer(
            directory,
            ingestion,
            NullLogger<NotificationEventRelayConsumer>.Instance);

        var entry = SampleEntry("TOPIC_SUBMITTED") with
        {
            RecipientUserIds = [onlyActor],
            ExcludeUserIds = [onlyActor]
        };

        await consumer.ConsumeAsync(new NotificationIntegrationEvent(
            Guid.NewGuid(),
            "TopicCatalog",
            "TOPIC_SUBMITTED",
            1,
            entry,
            NowUtc));

        Assert.Empty(ingestion.CapturedRequests);
    }

    [Fact]
    public void Runtime_DI_registers_all_four_module_outboxes_event_sources_and_relay_services()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CONNECTION_STRING"] = "Host=localhost;Database=test;Username=test"
        }).Build();

        using var provider = new ServiceCollection()
            .AddUgtmModules(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<INotificationRecipientDirectory>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<INotificationEventRelayConsumer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAcademicPeriodsNotificationOutbox>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITopicCatalogNotificationOutbox>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IThesisNotificationOutbox>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDefenseCouncilNotificationOutbox>());

        var sources = scope.ServiceProvider
            .GetServices<INotificationIntegrationEventSource>()
            .Select(s => s.SourceName)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["AcademicPeriods", "DefenseCouncil", "ThesisManagement", "TopicCatalog"],
            sources);
    }

    private static NotificationOutboxEntry SampleEntry(string notificationType) => new(
        NotificationType: notificationType,
        Title: "Tiêu đề thông báo thử nghiệm",
        Content: "Nội dung thông báo thử nghiệm.",
        RecipientRoles: ["Student"],
        ActionPath: "/notifications",
        EnqueueEmail: true,
        EmailMetadata: [new NotificationEmailMetaItem("Mã sự kiện", notificationType)]);

    private static void AssertEnvelope(Guid expectedEventId, string expectedModule, string expectedType, string payloadJson)
    {
        var envelope = JsonSerializer.Deserialize<NotificationIntegrationEvent>(payloadJson);
        Assert.NotNull(envelope);
        Assert.Equal(expectedEventId, envelope.EventId);
        Assert.Equal(expectedModule, envelope.SourceModule);
        Assert.Equal(expectedType, envelope.EventType);
        Assert.Equal(1, envelope.PayloadVersion);
        Assert.Equal(NowUtc, envelope.OccurredAtUtc);
        Assert.Equal(expectedType, envelope.Entry.NotificationType);
    }

    private sealed class FakeRecipientDirectory(IReadOnlyList<NotificationRecipientContractDto> recipients)
        : INotificationRecipientDirectory
    {
        public IReadOnlyCollection<Guid>? LastUserIds { get; private set; }
        public IReadOnlyCollection<string>? LastRoleNames { get; private set; }
        public IReadOnlyCollection<string>? LastPermissionCodes { get; private set; }

        public Task<IReadOnlyList<NotificationRecipientContractDto>> ResolveActiveRecipientsAsync(
            IReadOnlyCollection<Guid>? userIds = null,
            IReadOnlyCollection<string>? roleNames = null,
            IReadOnlyCollection<string>? permissionCodes = null,
            CancellationToken cancellationToken = default)
        {
            LastUserIds = userIds;
            LastRoleNames = roleNames;
            LastPermissionCodes = permissionCodes;
            return Task.FromResult(recipients);
        }
    }

    private sealed class CapturingIngestionService : INotificationIngestionService
    {
        public List<NotificationIngestionRequest> CapturedRequests { get; } = [];

        public Task<NotificationIngestionResult> IngestAsync(
            NotificationIngestionRequest request,
            CancellationToken cancellationToken = default)
        {
            CapturedRequests.Add(request);
            return Task.FromResult(new NotificationIngestionResult(
                Guid.NewGuid(),
                Deduplicated: false,
                DeliveriesEnqueued: request.EnqueueEmail ? 1 : 0));
        }

        public async Task<IReadOnlyList<NotificationIngestionResult>> IngestBatchAsync(
            IReadOnlyCollection<NotificationIngestionRequest> requests,
            CancellationToken cancellationToken = default)
        {
            var results = new List<NotificationIngestionResult>(requests.Count);
            foreach (var request in requests)
            {
                results.Add(await IngestAsync(request, cancellationToken));
            }

            return results;
        }
    }
}
