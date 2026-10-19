using Microsoft.Extensions.Options;
using UGTM.Modules.Identity.Infrastructure.Notifications;
using UGTM.Modules.Notifications.Contracts.Email;

namespace UGTM.Modules.Identity.Tests.Infrastructure;

public sealed class IdentityEmailTemplateRendererTests
{
    [Fact]
    public void Activation_email_uses_attachment_free_branding_and_safe_absolute_link()
    {
        var renderer = CreateRenderer(new Uri("https://portal.ugtm.edu.vn"));
        var payload = new IdentityEmailPayload(
            "student@ugtm.edu.vn",
            "<script>alert('x')</script>",
            "activation",
            "token/value+one");

        var email = renderer.Render(payload);

        Assert.Equal("Kích hoạt tài khoản UGTM", email.Subject);
        Assert.Contains("#2054A5", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("#FA7921", email.HtmlBody, StringComparison.Ordinal);
        AssertHtmlBrandingDoesNotDependOnImages(email);
        Assert.Contains(
            "https://portal.ugtm.edu.vn/activate#token=token%2Fvalue%2Bone",
            email.HtmlBody,
            StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", email.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token%2Fvalue%2Bone", email.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Recovery_email_highlights_otp_without_exposing_it_in_subject()
    {
        var renderer = CreateRenderer(new Uri("https://portal.ugtm.edu.vn"));
        var payload = new IdentityEmailPayload(
            "lecturer@ugtm.edu.vn",
            "Nguyễn Văn A",
            "recovery-otp",
            "012345");

        var email = renderer.Render(payload);

        Assert.Equal("Mã khôi phục mật khẩu UGTM", email.Subject);
        Assert.DoesNotContain("012345", email.Subject, StringComparison.Ordinal);
        Assert.Contains("012345", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("012345", email.TextBody, StringComparison.Ordinal);
        AssertHtmlBrandingDoesNotDependOnImages(email);
        Assert.Contains("Không chia sẻ mã", email.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_template_is_rejected()
    {
        var renderer = CreateRenderer(new Uri("https://portal.ugtm.edu.vn"));
        var payload = new IdentityEmailPayload(
            "student@ugtm.edu.vn",
            "Student",
            "unknown",
            "secret");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            renderer.Render(payload));

        Assert.Equal("Unknown email template.", exception.Message);
    }

    private static IdentityEmailTemplateRenderer CreateRenderer(Uri publicWebBaseUrl)
    {
        return new IdentityEmailTemplateRenderer(Options.Create(new IdentityEmailOptions
        {
            PublicWebBaseUrl = publicWebBaseUrl,
            OutboxPartition = "template-tests"
        }));
    }

    private static void AssertHtmlBrandingDoesNotDependOnImages(EmailMessage email)
    {
        Assert.Null(email.InlineImages);
        Assert.DoesNotContain("<img", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cid:", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-label=\"UGTM\"", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(">U</span><span", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(">G</span><span", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(">T</span><span", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(">M</span>", email.HtmlBody, StringComparison.Ordinal);
    }
}
