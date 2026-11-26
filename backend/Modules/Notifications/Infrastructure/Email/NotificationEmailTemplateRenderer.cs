using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.Notifications.Infrastructure.Delivery;

namespace UGTM.Modules.Notifications.Infrastructure.Email;

public sealed record NotificationEmailTemplateModel(
    string To,
    string Subject,
    string Title,
    string Content,
    string NotificationType,
    string Priority = "Normal",
    string? RecipientFullName = null,
    string? CategoryLabel = null,
    string? ActionPath = null,
    string? CtaLabel = null,
    string? Notice = null,
    IReadOnlyList<NotificationEmailMetaItem>? Metadata = null,
    IReadOnlyList<NotificationEmailDigestSection>? DigestSections = null,
    string? PreRenderedHtmlBody = null,
    string? PreRenderedTextBody = null);

public interface INotificationEmailTemplateRenderer
{
    EmailMessage Render(NotificationEmailTemplateModel model);
}

public sealed class NotificationEmailTemplateRenderer(
    IOptions<NotificationDeliveryOptions>? configuredOptions = null) : INotificationEmailTemplateRenderer
{
    private const string BrandBlue = "#2054A5";
    private const string BrandOrange = "#FA7921";
    private const string UrgentRed = "#E02424";
    private const string ProductName = "Hệ thống quản lý tốt nghiệp";

    private readonly NotificationDeliveryOptions _options =
        configuredOptions?.Value ?? new NotificationDeliveryOptions();

    public EmailMessage Render(NotificationEmailTemplateModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.To);

        var subject = NormalizeSubject(
            string.IsNullOrWhiteSpace(model.Subject) ? model.Title : model.Subject);

        if (!string.IsNullOrWhiteSpace(model.PreRenderedHtmlBody))
        {
            return new EmailMessage(
                model.To.Trim(),
                subject,
                model.PreRenderedHtmlBody,
                string.IsNullOrWhiteSpace(model.PreRenderedTextBody)
                    ? model.Content
                    : model.PreRenderedTextBody);
        }

        return model.DigestSections is { Count: > 0 }
            ? RenderDigest(model, subject)
            : RenderStandard(model, subject);
    }

    private EmailMessage RenderStandard(NotificationEmailTemplateModel model, string subject)
    {
        var isDanger = IsDangerEvent(model.NotificationType);
        var eyebrowColor = isDanger ? UrgentRed : "#697E9F";
        var eyebrow = Html(
            string.IsNullOrWhiteSpace(model.CategoryLabel)
                ? ResolveDefaultCategoryLabel(model.NotificationType)
                : model.CategoryLabel.Trim());
        var title = Html(model.Title.Trim());
        var greetingHtml = BuildGreetingHtml(model.RecipientFullName);
        var bodyParagraphsHtml = BuildBodyParagraphsHtml(model.Content);
        var metadataHtml = BuildMetadataTableHtml(model.Metadata, isDanger ? UrgentRed : BrandBlue);
        var actionUrl = ResolvePortalUrl(model.ActionPath);
        var ctaLabel = string.IsNullOrWhiteSpace(model.CtaLabel)
            ? "Xem chi tiết trên UGTM"
            : model.CtaLabel.Trim();
        var ctaHtml = BuildCtaHtml(actionUrl, ctaLabel);
        var noticeHtml = string.IsNullOrWhiteSpace(model.Notice)
            ? string.Empty
            : isDanger
                ? NoticeBox("Lưu ý quan trọng", model.Notice.Trim(), "#FDF2F2", "#F8B4B4", "#9B1C1C", "#C81E1E")
                : NoticeBox("Lưu ý", model.Notice.Trim());

        var contentHtml = $$"""
            <p style="margin:0 0 10px;color:{{eyebrowColor}};font-size:13px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">{{eyebrow}}</p>
            <h1 style="margin:0 0 18px;color:#10182C;font-size:26px;line-height:1.3;font-weight:750;">{{title}}</h1>
            {{greetingHtml}}
            {{bodyParagraphsHtml}}
            {{metadataHtml}}
            {{ctaHtml}}
            {{noticeHtml}}
            """;

        var textBody = !string.IsNullOrWhiteSpace(model.PreRenderedTextBody)
            ? model.PreRenderedTextBody
            : BuildStandardTextBody(model, actionUrl);

        return new EmailMessage(
            model.To.Trim(),
            subject,
            Layout(model.Title.Trim(), contentHtml),
            textBody);
    }

    private EmailMessage RenderDigest(NotificationEmailTemplateModel model, string subject)
    {
        var eyebrow = Html(
            string.IsNullOrWhiteSpace(model.CategoryLabel)
                ? "Tổng hợp nhắc hạn 24 giờ"
                : model.CategoryLabel.Trim());
        var title = Html(model.Title.Trim());
        var greetingHtml = BuildGreetingHtml(model.RecipientFullName);
        var bodyParagraphsHtml = BuildBodyParagraphsHtml(model.Content);

        var sectionsSb = new StringBuilder();
        foreach (var section in model.DigestSections!)
        {
            var sectionTitle = Html(section.Title.Trim());
            var sectionSummary = Html(section.Summary.Trim());
            var sectionUrl = ResolvePortalUrl(section.ActionPath);

            sectionsSb.Append($$"""
                <div style="margin:0 0 16px;padding:16px 18px;background:#F7F9FC;border:1px solid #E0E7F1;border-left:5px solid {{BrandBlue}};border-radius:10px;">
                  <div style="margin:0 0 6px;color:#10182C;font-size:16px;font-weight:700;line-height:1.4;">{{sectionTitle}}</div>
                  <div style="margin:0 0 8px;color:#1B2940;font-size:14px;line-height:1.6;">{{sectionSummary}}</div>
                """);

            if (section.Items is { Count: > 0 })
            {
                sectionsSb.Append("<ul style=\"margin:6px 0 10px;padding-left:20px;color:#1B2940;font-size:14px;line-height:1.65;\">");
                foreach (var item in section.Items.Where(i => !string.IsNullOrWhiteSpace(i)))
                {
                    sectionsSb.Append($"<li style=\"margin:0 0 4px;\">{Html(item.Trim())}</li>");
                }
                sectionsSb.Append("</ul>");
            }

            if (sectionUrl is not null)
            {
                sectionsSb.Append(
                    $"<div style=\"margin-top:8px;\"><a href=\"{Html(sectionUrl)}\" style=\"color:{BrandBlue};font-size:13px;font-weight:700;text-decoration:none;\">Mở trang xử lý&nbsp;&rarr;</a></div>");
            }

            sectionsSb.Append("</div>");
        }

        var mainActionUrl = ResolvePortalUrl(model.ActionPath);
        var mainCtaHtml = BuildCtaHtml(
            mainActionUrl,
            string.IsNullOrWhiteSpace(model.CtaLabel) ? "Mở hệ thống UGTM" : model.CtaLabel.Trim());
        var noticeHtml = string.IsNullOrWhiteSpace(model.Notice)
            ? string.Empty
            : NoticeBox("Lưu ý", model.Notice.Trim());

        var contentHtml = $$"""
            <p style="margin:0 0 10px;color:#697E9F;font-size:13px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">{{eyebrow}}</p>
            <h1 style="margin:0 0 18px;color:#10182C;font-size:26px;line-height:1.3;font-weight:750;">{{title}}</h1>
            {{greetingHtml}}
            {{bodyParagraphsHtml}}
            <div style="margin:0 0 24px;">{{sectionsSb}}</div>
            {{mainCtaHtml}}
            {{noticeHtml}}
            """;

        var textBody = !string.IsNullOrWhiteSpace(model.PreRenderedTextBody)
            ? model.PreRenderedTextBody
            : BuildDigestTextBody(model, mainActionUrl);

        return new EmailMessage(
            model.To.Trim(),
            subject,
            Layout(model.Title.Trim(), contentHtml),
            textBody);
    }

    private static string BuildGreetingHtml(string? recipientFullName)
    {
        if (string.IsNullOrWhiteSpace(recipientFullName))
        {
            return string.Empty;
        }

        return $"<p style=\"margin:0 0 14px;color:#1B2940;font-size:16px;line-height:1.7;\">Xin chào <strong>{Html(recipientFullName.Trim())}</strong>,</p>";
    }

    private static string BuildBodyParagraphsHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var sb = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            var bottomMargin = i == lines.Length - 1 ? 22 : 12;
            sb.Append($"<p style=\"margin:0 0 {bottomMargin}px;color:#1B2940;font-size:16px;line-height:1.7;\">{Html(lines[i])}</p>");
        }

        return sb.ToString();
    }

    private static string BuildMetadataTableHtml(
        IReadOnlyList<NotificationEmailMetaItem>? metadata,
        string accentColor)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return string.Empty;
        }

        var validItems = metadata
            .Where(m => !string.IsNullOrWhiteSpace(m.Label) && !string.IsNullOrWhiteSpace(m.Value))
            .ToArray();
        if (validItems.Length == 0)
        {
            return string.Empty;
        }

        var rows = new StringBuilder();
        for (var i = 0; i < validItems.Length; i++)
        {
            var item = validItems[i];
            var borderBottom = i < validItems.Length - 1 ? "border-bottom:1px solid #E8EDF4;" : string.Empty;
            rows.Append($$"""
                <tr>
                  <td valign="top" style="padding:8px 12px 8px 0;{{borderBottom}}color:#697E9F;font-size:13px;font-weight:600;width:36%;">{{Html(item.Label.Trim())}}</td>
                  <td valign="top" style="padding:8px 0;{{borderBottom}}color:#10182C;font-size:14px;font-weight:600;line-height:1.5;">{{Html(item.Value.Trim())}}</td>
                </tr>
                """);
        }

        return $$"""
            <div style="margin:0 0 24px;padding:16px 20px;background:#F7F9FC;border:1px solid #E0E7F1;border-left:5px solid {{accentColor}};border-radius:10px;">
              <table role="presentation" width="100%" border="0" cellpadding="0" cellspacing="0" style="width:100%;border-collapse:collapse;">
                {{rows}}
              </table>
            </div>
            """;
    }

    private static string BuildCtaHtml(string? actionUrl, string ctaLabel)
    {
        if (string.IsNullOrWhiteSpace(actionUrl))
        {
            return string.Empty;
        }

        var encodedUrl = Html(actionUrl);
        var encodedLabel = Html(ctaLabel);
        return $$"""
            <table role="presentation" border="0" cellpadding="0" cellspacing="0" style="margin:0 0 22px;">
              <tr>
                <td bgcolor="{{BrandBlue}}" style="border-radius:10px;background-color:{{BrandBlue}};background-image:linear-gradient(90deg,{{BrandBlue}} 0%,{{BrandOrange}} 100%);">
                  <a href="{{encodedUrl}}" style="display:inline-block;padding:14px 28px;color:#FFFFFF;font-size:16px;font-weight:700;line-height:20px;text-decoration:none;border-radius:10px;">{{encodedLabel}}&nbsp;&nbsp;&rarr;</a>
                </td>
              </tr>
            </table>
            <p style="margin:0 0 8px;color:#697E9F;font-size:13px;line-height:1.6;">Nếu nút không hoạt động, hãy sao chép liên kết sau vào trình duyệt:</p>
            <div style="margin:0 0 24px;padding:12px 15px;background:#F7F9FC;border:1px solid #E0E7F1;border-radius:8px;color:{{BrandBlue}};font-size:12px;line-height:1.6;word-break:break-all;">{{encodedUrl}}</div>
            """;
    }

    private static string BuildStandardTextBody(NotificationEmailTemplateModel model, string? actionUrl)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(model.RecipientFullName))
        {
            sb.AppendLine($"Xin chào {model.RecipientFullName.Trim()},");
            sb.AppendLine();
        }

        sb.AppendLine(model.Title.Trim());
        sb.AppendLine();
        sb.AppendLine(model.Content.Trim());

        if (model.Metadata is { Count: > 0 })
        {
            sb.AppendLine();
            foreach (var item in model.Metadata.Where(m => !string.IsNullOrWhiteSpace(m.Label) && !string.IsNullOrWhiteSpace(m.Value)))
            {
                sb.AppendLine($"- {item.Label.Trim()}: {item.Value.Trim()}");
            }
        }

        if (!string.IsNullOrWhiteSpace(actionUrl))
        {
            sb.AppendLine();
            sb.AppendLine($"Xem chi tiết tại: {actionUrl}");
        }

        if (!string.IsNullOrWhiteSpace(model.Notice))
        {
            sb.AppendLine();
            sb.AppendLine($"Lưu ý: {model.Notice.Trim()}");
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildDigestTextBody(NotificationEmailTemplateModel model, string? mainActionUrl)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(model.RecipientFullName))
        {
            sb.AppendLine($"Xin chào {model.RecipientFullName.Trim()},");
            sb.AppendLine();
        }

        sb.AppendLine(model.Title.Trim());
        sb.AppendLine();
        sb.AppendLine(model.Content.Trim());

        foreach (var section in model.DigestSections!)
        {
            sb.AppendLine();
            sb.AppendLine($"* {section.Title.Trim()}: {section.Summary.Trim()}");
            if (section.Items is { Count: > 0 })
            {
                foreach (var item in section.Items.Where(i => !string.IsNullOrWhiteSpace(i)))
                {
                    sb.AppendLine($"  - {item.Trim()}");
                }
            }

            var sectionUrl = ResolvePortalUrl(section.ActionPath);
            if (!string.IsNullOrWhiteSpace(sectionUrl))
            {
                sb.AppendLine($"  Liên kết: {sectionUrl}");
            }
        }

        if (!string.IsNullOrWhiteSpace(mainActionUrl))
        {
            sb.AppendLine();
            sb.AppendLine($"Truy cập hệ thống: {mainActionUrl}");
        }

        if (!string.IsNullOrWhiteSpace(model.Notice))
        {
            sb.AppendLine();
            sb.AppendLine($"Lưu ý: {model.Notice.Trim()}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string Layout(string preheader, string content)
    {
        return $$"""
            <!doctype html>
            <html lang="vi">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <meta name="x-apple-disable-message-reformatting">
              <title>UGTM</title>
            </head>
            <body style="margin:0;padding:0;background:#F1F3F6;font-family:Inter,-apple-system,BlinkMacSystemFont,'Segoe UI',Arial,sans-serif;-webkit-text-size-adjust:100%;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{{Html(preheader)}}&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;</div>
              <table role="presentation" width="100%" border="0" cellpadding="0" cellspacing="0" bgcolor="#F1F3F6" style="width:100%;background:#F1F3F6;">
                <tr>
                  <td align="center" style="padding:36px 14px;">
                    <table role="presentation" width="600" border="0" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;background:#FFFFFF;border:1px solid #E0E7F1;border-radius:14px;overflow:hidden;box-shadow:0 8px 24px rgba(16,24,44,.06);">
                      <tr>
                        <td height="7" bgcolor="{{BrandBlue}}" style="height:7px;font-size:0;line-height:0;background-color:{{BrandBlue}};background-image:linear-gradient(90deg,{{BrandBlue}} 0%,{{BrandOrange}} 100%);">&nbsp;</td>
                      </tr>
                      <tr>
                        <td style="padding:25px 34px;border-bottom:1px solid #E8EDF4;">
                          <table role="presentation" width="100%" border="0" cellpadding="0" cellspacing="0">
                            <tr>
                              <td valign="middle">
                                <span aria-label="UGTM" style="display:inline-block;font-family:Arial,sans-serif;font-size:27px;font-weight:800;line-height:34px;letter-spacing:-1.5px;white-space:nowrap;"><span style="color:#2054A5;">U</span><span style="color:#24324A;">G</span><span style="color:#C97852;">T</span><span style="color:#FA7921;">M</span></span>
                              </td>
                              <td align="right" valign="middle" style="padding-left:18px;color:#697E9F;font-size:12px;line-height:1.45;">{{ProductName}}</td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:38px 34px 34px;">{{content}}</td>
                      </tr>
                      <tr>
                        <td style="padding:21px 34px;background:#F7F9FC;border-top:1px solid #E8EDF4;color:#697E9F;font-size:12px;line-height:1.6;text-align:center;">
                          Email được gửi tự động từ UGTM. Vui lòng không trả lời email này.<br>
                          © {{DateTime.UtcNow.Year}} UGTM - {{ProductName}}
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string NoticeBox(
        string title,
        string message,
        string bgColor = "#FFF8F2",
        string borderColor = "#FADBC5",
        string textColor = "#7A431D",
        string titleColor = "#B65311")
    {
        return $$"""
            <table role="presentation" width="100%" border="0" cellpadding="0" cellspacing="0" style="width:100%;">
              <tr>
                <td style="padding:14px 16px;background:{{bgColor}};border:1px solid {{borderColor}};border-radius:9px;color:{{textColor}};font-size:13px;line-height:1.6;">
                  <strong style="color:{{titleColor}};">{{Html(title)}}:</strong> {{Html(message)}}
                </td>
              </tr>
            </table>
            """;
    }

    private string? ResolvePortalUrl(string? actionPath)
    {
        if (string.IsNullOrWhiteSpace(actionPath))
        {
            return null;
        }

        var trimmed = actionPath.Trim();
        // Only allow safe relative application paths starting with a single '/'
        if (!trimmed.StartsWith('/') || trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        var baseUrl = string.IsNullOrWhiteSpace(_options.PublicWebBaseUrl)
            ? "http://localhost:5173"
            : _options.PublicWebBaseUrl.Trim();

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        return new Uri(baseUri, trimmed).AbsoluteUri;
    }

    private static string NormalizeSubject(string rawSubject)
    {
        var cleaned = rawSubject
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return cleaned.StartsWith("[UGTM]", StringComparison.OrdinalIgnoreCase)
            ? cleaned
            : $"[UGTM] {cleaned}";
    }

    private static bool IsDangerEvent(string notificationType)
        => string.Equals(notificationType, "GROUP_STOPPED", StringComparison.OrdinalIgnoreCase);

    private static string ResolveDefaultCategoryLabel(string notificationType)
    {
        if (string.IsNullOrWhiteSpace(notificationType))
        {
            return "Thông báo hệ thống";
        }

        var upper = notificationType.Trim().ToUpperInvariant();
        if (upper.EndsWith("_DEADLINE_SOON", StringComparison.Ordinal))
            return "Nhắc gần đến hạn";
        if (upper == "SUBMISSION_CONFIRMED")
            return "Xác nhận nộp hồ sơ";
        if (upper.StartsWith("COUNCIL_", StringComparison.Ordinal)
            || upper.StartsWith("REVIEWER_", StringComparison.Ordinal)
            || upper == "ASSIGNMENT_CANCELLED")
            return "Hội đồng & Phản biện";
        if (upper == "GROUP_STOPPED")
            return "Thông báo học vụ quan trọng";
        if (upper == "TOPIC_REVISION_REQUESTED")
            return "Thẩm định đề tài KLTN";
        if (upper == "SCORE_SUMMARY_PUBLISHED")
            return "Công bố kết quả KLTN";

        return "Thông báo từ UGTM";
    }

    private static string Html(string value)
        => value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
}
