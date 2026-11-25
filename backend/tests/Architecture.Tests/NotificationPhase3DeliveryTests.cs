using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.Notifications.Domain.Entities;
using UGTM.Modules.Notifications.Infrastructure.Delivery;
using UGTM.Modules.Notifications.Infrastructure.Email;

namespace UGTM.Architecture.Tests;

public sealed class NotificationPhase3DeliveryTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 25, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TemplateRenderer_renders_standard_email_with_xss_encoding_safe_action_url_and_text_fallback()
    {
        var renderer = new NotificationEmailTemplateRenderer(Options.Create(new NotificationDeliveryOptions
        {
            PublicWebBaseUrl = "https://ugtm.uit.edu.vn"
        }));

        var email = renderer.Render(new NotificationEmailTemplateModel(
            To: "sv@gm.uit.edu.vn",
            Subject: "Xác nhận nộp Đề cương v1",
            Title: "Đã ghi nhận Đề cương <script>alert(1)</script>",
            Content: "Nhóm của bạn vừa nộp thành công tệp đề cương.\nVui lòng theo dõi kết quả thẩm định.",
            NotificationType: "SUBMISSION_CONFIRMED",
            Priority: NotificationPriorities.Critical,
            RecipientFullName: "Nguyễn Văn A",
            ActionPath: "/thesis/proposal",
            CtaLabel: "Mở trang đề cương",
            Notice: "Bạn có thể nộp lại nếu Khoa yêu cầu chỉnh sửa.",
            Metadata:
            [
                new NotificationEmailMetaItem("Nhóm", "Nhóm G01"),
                new NotificationEmailMetaItem("Tệp đính kèm", "De-cuong-v1.pdf (1.4 MB)")
            ]));

        Assert.Equal("[UGTM] Xác nhận nộp Đề cương v1", email.Subject);
        Assert.Contains("Xác nhận nộp hồ sơ", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", email.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert(1)</script>", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("https://ugtm.uit.edu.vn/thesis/proposal", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("De-cuong-v1.pdf (1.4 MB)", email.HtmlBody, StringComparison.Ordinal);
        Assert.NotNull(email.TextBody);
        Assert.Contains("Xin chào Nguyễn Văn A,", email.TextBody, StringComparison.Ordinal);
        Assert.Contains("https://ugtm.uit.edu.vn/thesis/proposal", email.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateRenderer_blocks_unsafe_external_action_paths_and_uses_danger_palette_for_group_stopped()
    {
        var renderer = new NotificationEmailTemplateRenderer(Options.Create(new NotificationDeliveryOptions
        {
            PublicWebBaseUrl = "https://ugtm.uit.edu.vn"
        }));

        var email = renderer.Render(new NotificationEmailTemplateModel(
            To: "sv@gm.uit.edu.vn",
            Subject: "[UGTM] Nhóm dừng thực hiện đề tài",
            Title: "Nhóm đã dừng thực hiện đề tài",
            Content: "Hồ sơ không đạt yêu cầu ở Cổng 1.",
            NotificationType: "GROUP_STOPPED",
            ActionPath: "//evil.example.com/phish",
            Notice: "Liên hệ Văn phòng Khoa nếu cần giải đáp."));

        Assert.Equal("[UGTM] Nhóm dừng thực hiện đề tài", email.Subject);
        Assert.DoesNotContain("evil.example.com", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("#FDF2F2", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("#E02424", email.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateRenderer_renders_24h_staff_digest_with_multiple_sections_and_links()
    {
        var renderer = new NotificationEmailTemplateRenderer(Options.Create(new NotificationDeliveryOptions
        {
            PublicWebBaseUrl = "https://ugtm.uit.edu.vn"
        }));

        var email = renderer.Render(new NotificationEmailTemplateModel(
            To: "khoa@uit.edu.vn",
            Subject: "Nhắc hạn 24h xử lý hồ sơ KLTN",
            Title: "Tổng hợp hồ sơ sắp đến hạn trong 24 giờ",
            Content: "Các hạng mục dưới đây thuộc đợt KLTN hiện hành cần Khoa xử lý trước hạn chót.",
            NotificationType: "SYLLABUS_FACULTY_RESPONSE_DEADLINE_SOON",
            RecipientFullName: "Ban chủ nhiệm Khoa",
            DigestSections:
            [
                new NotificationEmailDigestSection(
                    "Đề cương chờ thẩm định",
                    "Còn 8 hồ sơ đang chờ phản hồi trước 23:59 26/11/2026.",
                    "/thesis/review/syllabus",
                    ["Nhóm G01 - GraphRAG UIT", "Nhóm G04 - AI Agent"]),
                new NotificationEmailDigestSection(
                    "Đề tài chờ duyệt",
                    "Còn 3 đề tài mới từ giảng viên.",
                    "/topics/review")
            ]));

        Assert.Contains("Tổng hợp nhắc hạn 24 giờ", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Đề cương chờ thẩm định", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Nhóm G01 - GraphRAG UIT", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("https://ugtm.uit.edu.vn/thesis/review/syllabus", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("https://ugtm.uit.edu.vn/topics/review", email.HtmlBody, StringComparison.Ordinal);
        Assert.NotNull(email.TextBody);
        Assert.Contains("* Đề cương chờ thẩm định", email.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotificationDeliveryDispatcher_batches_emails_applies_backoff_permanent_failures_and_skips_disabled_fcm()
    {
        var okId = Guid.NewGuid();
        var transientId = Guid.NewGuid();
        var exhaustedId = Guid.NewGuid();
        var permanentId = Guid.NewGuid();
        var fcmId = Guid.NewGuid();

        var queueStore = new FakeDeliveryQueueStore(
        [
            CreateClaimedEmail(okId, "ok@gm.uit.edu.vn", attemptCount: 0),
            CreateClaimedEmail(transientId, "temp@gm.uit.edu.vn", attemptCount: 1),
            CreateClaimedEmail(exhaustedId, "maxed@gm.uit.edu.vn", attemptCount: 4),
            CreateClaimedEmail(permanentId, "missing@gm.uit.edu.vn", attemptCount: 0),
            new ClaimedNotificationDelivery(
                fcmId, Guid.NewGuid(), Guid.NewGuid(),
                NotificationChannels.Fcm, Guid.NewGuid().ToString(),
                AttemptCount: 0, PayloadJson: "{}",
                "Push title", "Push body", "TEST", NotificationPriorities.Normal, "/thesis/proposal")
        ]);

        var emailSender = new FakeBatchEmailSender(item => item.Message.To switch
        {
            "ok@gm.uit.edu.vn" => new EmailBatchItemResult(item.Id, Succeeded: true),
            "temp@gm.uit.edu.vn" => new EmailBatchItemResult(item.Id, Succeeded: false, IsPermanentFailure: false, ErrorCode: "SmtpTimeout", ErrorMessage: "Timed out"),
            "maxed@gm.uit.edu.vn" => new EmailBatchItemResult(item.Id, Succeeded: false, IsPermanentFailure: false, ErrorCode: "SmtpTimeout", ErrorMessage: "Timed out"),
            "missing@gm.uit.edu.vn" => new EmailBatchItemResult(item.Id, Succeeded: false, IsPermanentFailure: true, ErrorCode: "Smtp550", ErrorMessage: "User unknown"),
            _ => throw new InvalidOperationException()
        });

        var options = Options.Create(new NotificationDeliveryOptions
        {
            Enabled = true,
            PublicWebBaseUrl = "https://ugtm.uit.edu.vn",
            BatchSize = 25,
            MaxAttempts = 5,
            FcmEnabled = false
        });

        var dispatcher = new NotificationDeliveryDispatcher(
            queueStore,
            emailSender,
            new NotificationEmailTemplateRenderer(options),
            options,
            new FixedTimeProvider(NowUtc),
            NullLogger<NotificationDeliveryDispatcher>.Instance);

        var processed = await dispatcher.DispatchNextBatchAsync();

        Assert.Equal(5, processed);
        Assert.Equal(1, emailSender.BatchCallCount);
        Assert.Equal(4, emailSender.LastBatchCount);

        var outcomes = queueStore.Outcomes.ToDictionary(o => o.DeliveryId);

        Assert.Equal(NotificationDeliveryStatuses.Accepted, outcomes[okId].Status);
        Assert.Equal(1, outcomes[okId].AttemptCount);
        Assert.Equal(NowUtc, outcomes[okId].SentAtUtc);

        // attemptCount was 1 -> nextAttemptCount = 2 -> backoff = 5 minutes
        Assert.Equal(NotificationDeliveryStatuses.RetryScheduled, outcomes[transientId].Status);
        Assert.Equal(2, outcomes[transientId].AttemptCount);
        Assert.Equal(NowUtc.AddMinutes(5), outcomes[transientId].NextAttemptAtUtc);

        // attemptCount was 4 -> nextAttemptCount = 5 >= MaxAttempts (5) -> FailedPermanent
        Assert.Equal(NotificationDeliveryStatuses.FailedPermanent, outcomes[exhaustedId].Status);
        Assert.Equal(5, outcomes[exhaustedId].AttemptCount);

        // Permanent 550 on first attempt -> FailedPermanent immediately
        Assert.Equal(NotificationDeliveryStatuses.FailedPermanent, outcomes[permanentId].Status);
        Assert.Equal(1, outcomes[permanentId].AttemptCount);
        Assert.Equal("Smtp550", outcomes[permanentId].ErrorCode);

        // FCM disabled -> Skipped cleanly
        Assert.Equal(NotificationDeliveryStatuses.Skipped, outcomes[fcmId].Status);
        Assert.Equal("FcmDisabled", outcomes[fcmId].ErrorCode);
    }

    private static ClaimedNotificationDelivery CreateClaimedEmail(Guid id, string to, int attemptCount)
        => new(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationChannels.Email,
            to,
            attemptCount,
            JsonSerializer.Serialize(new { subject = "Thông báo KLTN", recipientFullName = "Sinh viên UIT" }),
            "Tiêu đề thông báo",
            "Nội dung chi tiết thông báo.",
            "SYLLABUS_INITIAL_DEADLINE_SOON",
            NotificationPriorities.Critical,
            "/thesis/proposal");

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakeDeliveryQueueStore(IReadOnlyList<ClaimedNotificationDelivery> batch)
        : INotificationDeliveryQueueStore
    {
        private bool _claimed;
        public List<NotificationDeliveryOutcome> Outcomes { get; } = [];

        public Task<IReadOnlyList<ClaimedNotificationDelivery>> ClaimBatchAsync(
            int batchSize, DateTime nowUtc, DateTime staleCutoffUtc, TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            if (_claimed) return Task.FromResult<IReadOnlyList<ClaimedNotificationDelivery>>([]);
            _claimed = true;
            return Task.FromResult(batch);
        }

        public Task ApplyOutcomesAsync(
            IReadOnlyCollection<NotificationDeliveryOutcome> outcomes,
            CancellationToken cancellationToken = default)
        {
            Outcomes.AddRange(outcomes);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBatchEmailSender(Func<EmailBatchItem, EmailBatchItemResult> responder) : IEmailSender
    {
        public int BatchCallCount { get; private set; }
        public int LastBatchCount { get; private set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
            => throw new NotSupportedException("Dispatcher should call SendBatchAsync.");

        public Task<IReadOnlyList<EmailBatchItemResult>> SendBatchAsync(
            IReadOnlyList<EmailBatchItem> items,
            CancellationToken cancellationToken = default)
        {
            BatchCallCount++;
            LastBatchCount = items.Count;
            return Task.FromResult<IReadOnlyList<EmailBatchItemResult>>(items.Select(responder).ToArray());
        }
    }
}
