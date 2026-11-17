using Microsoft.Extensions.Options;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Infrastructure.Notifications;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class ThesisEmailTemplateRendererTests
{
    private readonly ThesisEmailTemplateRenderer _renderer = new(
        Options.Create(new ThesisEmailOptions
        {
            PublicWebBaseUrl = new Uri("https://portal.ugtm.edu.vn")
        }));

    [Fact]
    public void Registration_invite_email_renders_branded_card_token_box_button_and_notice()
    {
        var expiresAtUtc = new DateTime(2026, 10, 25, 12, 50, 59, DateTimeKind.Utc);
        var email = _renderer.RenderTopicRegistrationInvite(
            to: "partner@test.edu.vn",
            partnerFullName: "Huỳnh Gia Thịnh",
            studentAName: "<script>alert('A')</script> Thịnh Huỳnh Gia",
            rawToken: "abc123token456xyz",
            expiresAtUtc: expiresAtUtc);

        Assert.Equal("[UGTM] Xác nhận tham gia nhóm đăng ký đề tài KLTN", email.Subject);
        AssertHtmlBranding(email);

        // Content verification
        Assert.Contains("Xác nhận tham gia nhóm KLTN", email.HtmlBody);
        Assert.Contains("Huỳnh Gia Thịnh", email.HtmlBody);
        Assert.Contains("&lt;script&gt;alert(&#39;A&#39;)&lt;/script&gt;", email.HtmlBody);
        Assert.DoesNotContain("<script>", email.HtmlBody, StringComparison.OrdinalIgnoreCase);

        // Token Box
        Assert.Contains("Mã xác nhận tham gia", email.HtmlBody);
        Assert.Contains("abc123token456xyz", email.HtmlBody);

        // CTA Button and Fallback URL
        Assert.Contains("https://portal.ugtm.edu.vn/topics/confirm#token=abc123token456xyz", email.HtmlBody);
        Assert.Contains("Xác nhận tham gia đề tài", email.HtmlBody);

        // Notice Box
        Assert.Contains("Lưu ý quan trọng", email.HtmlBody);
        Assert.Contains("2026-10-25 12:50:59 UTC", email.HtmlBody);
        Assert.Contains("1 lần vi phạm cảnh cáo", email.HtmlBody);

        // Plain text fallback
        Assert.NotNull(email.TextBody);
        Assert.Contains("Huỳnh Gia Thịnh", email.TextBody);
        Assert.Contains("abc123token456xyz", email.TextBody);
        Assert.Contains("2026-10-25 12:50:59 UTC", email.TextBody);
        Assert.Contains("https://portal.ugtm.edu.vn/topics/confirm#token=abc123token456xyz", email.TextBody);
    }

    [Fact]
    public void Registration_confirmation_for_inviter_renders_branded_card_and_status_box()
    {
        var expiresAtUtc = new DateTime(2026, 10, 25, 12, 50, 59, DateTimeKind.Utc);
        var email = _renderer.RenderTopicRegistrationConfirmationForInviter(
            to: "studentA@test.edu.vn",
            studentAFullName: "Thịnh Huỳnh Gia",
            partnerFullName: "Huỳnh Gia Thịnh",
            partnerUserCode: "24521680",
            expiresAtUtc: expiresAtUtc);

        Assert.Equal("[UGTM] Thông báo đăng ký đề tài khóa luận", email.Subject);
        AssertHtmlBranding(email);

        Assert.Contains("Đăng ký đề tài khóa luận thành công", email.HtmlBody);
        Assert.Contains("Thịnh Huỳnh Gia", email.HtmlBody);
        Assert.Contains("Huỳnh Gia Thịnh", email.HtmlBody);
        Assert.Contains("24521680", email.HtmlBody);
        Assert.Contains("tạm giữ chỗ đề tài", email.HtmlBody);
        Assert.Contains("2026-10-25 12:50:59 UTC", email.HtmlBody);
        Assert.Contains("Kiểm tra trạng thái đề tài", email.HtmlBody);
        Assert.Contains("https://portal.ugtm.edu.vn/topics", email.HtmlBody);

        // Plain text fallback
        Assert.NotNull(email.TextBody);
        Assert.Contains("Thịnh Huỳnh Gia", email.TextBody);
        Assert.Contains("Huỳnh Gia Thịnh", email.TextBody);
        Assert.Contains("24521680", email.TextBody);
        Assert.Contains("2026-10-25 12:50:59 UTC", email.TextBody);
    }

    [Fact]
    public void Advisor_assigned_notification_renders_branded_layout()
    {
        var email = _renderer.RenderAdvisorAssignedNotification(
            to: "student@test.edu.vn",
            studentFullName: "Nguyễn Văn C",
            studentUserCode: "24520001");

        Assert.Equal("[UGTM] Thông báo gán đề tài khóa luận từ GVHD", email.Subject);
        AssertHtmlBranding(email);
        Assert.Contains("Ghi nhận vào nhóm đề tài KLTN", email.HtmlBody);
        Assert.Contains("Nguyễn Văn C", email.HtmlBody);
        Assert.Contains("24520001", email.HtmlBody);
        Assert.Contains("xác nhận chính thức (Confirmed)", email.HtmlBody);
        Assert.Contains("https://portal.ugtm.edu.vn/topics", email.HtmlBody);
    }

    [Fact]
    public void Expiration_warning_renders_violation_count_and_notice()
    {
        var email = _renderer.RenderTopicRegistrationWarning(
            to: "student@test.edu.vn",
            studentFullName: "Trần Thị B",
            studentUserCode: "24520002",
            violationCount: 1,
            maxViolations: 3);

        Assert.Equal("[UGTM] Cảnh cáo: Quá hạn xác nhận tham gia đề tài (1/3)", email.Subject);
        AssertHtmlBranding(email);
        Assert.Contains("Quá hạn xác nhận tham gia đề tài", email.HtmlBody);
        Assert.Contains("Trần Thị B", email.HtmlBody);
        Assert.Contains("1/3", email.HtmlBody);
        Assert.Contains("Cảnh cáo vi phạm", email.HtmlBody);
    }

    [Fact]
    public void Expiration_blocked_renders_red_notice_and_locked_status()
    {
        var email = _renderer.RenderTopicRegistrationBlocked(
            to: "student@test.edu.vn",
            studentFullName: "Lê Văn D",
            studentUserCode: "24520003",
            violationCount: 3);

        Assert.Equal("[UGTM] Thông báo: Khóa quyền đăng ký đề tài khóa luận", email.Subject);
        AssertHtmlBranding(email);
        Assert.Contains("Khóa quyền đăng ký đề tài khóa luận", email.HtmlBody);
        Assert.Contains("L&#234; Văn D", email.HtmlBody);
        Assert.Contains("3 lần vi phạm", email.HtmlBody);
        Assert.Contains("#FDF2F2", email.HtmlBody); // Red background alert box

        // Plain text fallback
        Assert.NotNull(email.TextBody);
        Assert.Contains("Lê Văn D", email.TextBody);
        Assert.Contains("3 lần vi phạm", email.TextBody);
    }

    private static void AssertHtmlBranding(EmailMessage email)
    {
        Assert.Null(email.InlineImages);
        Assert.DoesNotContain("<img", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cid:", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-label=\"UGTM\"", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(">U</span><span", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(">G</span><span", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(">T</span><span", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(">M</span>", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("#2054A5", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("#FA7921", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Hệ thống quản lý tốt nghiệp", email.HtmlBody, StringComparison.Ordinal);
    }
}
