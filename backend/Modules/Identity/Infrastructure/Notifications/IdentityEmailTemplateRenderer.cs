using System.Net;
using Microsoft.Extensions.Options;
using UGTM.Modules.Notifications.Contracts.Email;

namespace UGTM.Modules.Identity.Infrastructure.Notifications;

public sealed class IdentityEmailTemplateRenderer(IOptions<IdentityEmailOptions> configuredOptions)
{
    private const string BrandBlue = "#2054A5";
    private const string BrandOrange = "#FA7921";
    private const string LogoContentId = "ugtm-brand-logo";
    private const string LogoResourceName = "UGTM.Identity.EmailAssets.ugtm-logo.png";
    private const string ProductName = "Hệ thống quản lý tốt nghiệp";
    private static readonly byte[] LogoContent = LoadLogo();
    private readonly IdentityEmailOptions options = configuredOptions.Value;

    public EmailMessage Render(IdentityEmailPayload payload)
    {
        return payload.Template switch
        {
            "activation" => RenderActivation(payload),
            "recovery-otp" => RenderRecoveryOtp(payload),
            _ => throw new InvalidOperationException("Unknown email template.")
        };
    }

    private EmailMessage RenderActivation(IdentityEmailPayload payload)
    {
        var fullName = Html(payload.FullName);
        var activationUrl = ActivationUrl(payload.Secret);
        var encodedActivationUrl = Html(activationUrl);
        var content = $$"""
            <p style="margin:0 0 10px;color:#697E9F;font-size:13px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">Chào mừng đến với UGTM</p>
            <h1 style="margin:0 0 18px;color:#10182C;font-size:28px;line-height:1.25;font-weight:750;">Kích hoạt tài khoản của bạn</h1>
            <p style="margin:0 0 14px;color:#1B2940;font-size:16px;line-height:1.7;">Xin chào <strong>{{fullName}}</strong>,</p>
            <p style="margin:0 0 26px;color:#1B2940;font-size:16px;line-height:1.7;">Tài khoản UGTM của bạn đã được khởi tạo. Hãy nhấn nút bên dưới để xác minh email và thiết lập mật khẩu lần đầu.</p>
            <table role="presentation" border="0" cellpadding="0" cellspacing="0" style="margin:0 0 26px;">
              <tr>
                <td bgcolor="{{BrandBlue}}" style="border-radius:10px;background-color:{{BrandBlue}};background-image:linear-gradient(90deg,{{BrandBlue}} 0%,{{BrandOrange}} 100%);">
                  <a href="{{encodedActivationUrl}}" style="display:inline-block;padding:14px 28px;color:#FFFFFF;font-size:16px;font-weight:700;line-height:20px;text-decoration:none;border-radius:10px;">Kích hoạt tài khoản&nbsp;&nbsp;→</a>
                </td>
              </tr>
            </table>
            <p style="margin:0 0 9px;color:#697E9F;font-size:13px;line-height:1.6;">Nếu nút không hoạt động, hãy sao chép liên kết sau vào trình duyệt:</p>
            <div style="margin:0 0 26px;padding:13px 15px;background:#F7F9FC;border:1px solid #E0E7F1;border-radius:8px;color:#2054A5;font-size:12px;line-height:1.6;word-break:break-all;">{{encodedActivationUrl}}</div>
            {{SecurityNotice("Liên kết này chỉ dùng được một lần và có thời hạn. Nếu bạn không yêu cầu tạo tài khoản, hãy bỏ qua email này.")}}
            """;

        return new EmailMessage(
            payload.To,
            "Kích hoạt tài khoản UGTM",
            Layout(
                "Tài khoản UGTM của bạn đã sẵn sàng để kích hoạt.",
                content),
            $"Xin chào {payload.FullName},\n\n"
            + "Tài khoản UGTM của bạn đã được khởi tạo. Kích hoạt và thiết lập mật khẩu tại:\n"
            + $"{activationUrl}\n\n"
            + "Liên kết chỉ dùng được một lần và có thời hạn. Nếu bạn không yêu cầu tạo tài khoản, hãy bỏ qua email này.",
            InlineLogo());
    }

    private EmailMessage RenderRecoveryOtp(IdentityEmailPayload payload)
    {
        var fullName = Html(payload.FullName);
        var secret = Html(payload.Secret);
        var content = $$"""
            <p style="margin:0 0 10px;color:#697E9F;font-size:13px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">Bảo mật tài khoản</p>
            <h1 style="margin:0 0 18px;color:#10182C;font-size:28px;line-height:1.25;font-weight:750;">Khôi phục mật khẩu</h1>
            <p style="margin:0 0 14px;color:#1B2940;font-size:16px;line-height:1.7;">Xin chào <strong>{{fullName}}</strong>,</p>
            <p style="margin:0 0 22px;color:#1B2940;font-size:16px;line-height:1.7;">Sử dụng mã xác thực bên dưới để tiếp tục đặt lại mật khẩu UGTM:</p>
            <div style="margin:0 0 22px;padding:22px 16px;background:#F7F9FC;border:1px solid #E0E7F1;border-left:5px solid {{BrandBlue}};border-radius:10px;text-align:center;">
              <div style="margin:0 0 7px;color:#697E9F;font-size:12px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;">Mã xác thực</div>
              <div style="color:#10182C;font-family:Consolas,'Courier New',monospace;font-size:34px;font-weight:800;letter-spacing:.18em;line-height:1.25;">{{secret}}</div>
            </div>
            <p style="margin:0 0 26px;color:#697E9F;font-size:14px;line-height:1.65;">Mã có thời hạn và chỉ dùng được một lần. Không chia sẻ mã này với bất kỳ ai, kể cả người tự xưng là nhân viên UGTM.</p>
            {{SecurityNotice("Nếu bạn không yêu cầu khôi phục mật khẩu, hãy bỏ qua email này. Mật khẩu hiện tại của bạn vẫn được giữ nguyên.")}}
            """;

        return new EmailMessage(
            payload.To,
            "Mã khôi phục mật khẩu UGTM",
            Layout("Mã xác thực khôi phục mật khẩu UGTM của bạn.", content),
            $"Xin chào {payload.FullName},\n\n"
            + $"Mã xác thực khôi phục mật khẩu UGTM của bạn là: {payload.Secret}\n\n"
            + "Mã có thời hạn và chỉ dùng được một lần. Không chia sẻ mã này với bất kỳ ai. "
            + "Nếu bạn không yêu cầu khôi phục mật khẩu, hãy bỏ qua email này.",
            InlineLogo());
    }

    private string Layout(string preheader, string content)
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
                                <img src="cid:{{LogoContentId}}" width="128" height="34" alt="UGTM" style="display:block;width:128px;height:34px;border:0;color:{{BrandBlue}};font-size:24px;font-weight:800;">
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
                          © {{DateTime.UtcNow.Year}} UGTM · {{ProductName}}
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

    private static string SecurityNotice(string message)
    {
        return $$"""
            <table role="presentation" width="100%" border="0" cellpadding="0" cellspacing="0" style="width:100%;">
              <tr>
                <td style="padding:14px 16px;background:#FFF8F2;border:1px solid #FADBC5;border-radius:9px;color:#7A431D;font-size:13px;line-height:1.6;">
                  <strong style="color:#B65311;">Lưu ý bảo mật:</strong> {{Html(message)}}
                </td>
              </tr>
            </table>
            """;
    }

    private string ActivationUrl(string token)
    {
        var builder = new UriBuilder(new Uri(options.PublicWebBaseUrl, "/activate"))
        {
            Fragment = $"token={Uri.EscapeDataString(token)}"
        };

        return builder.Uri.AbsoluteUri;
    }

    private static IReadOnlyCollection<EmailInlineImage> InlineLogo() =>
    [
        new EmailInlineImage(LogoContentId, "ugtm-logo.png", LogoContent)
    ];

    private static byte[] LoadLogo()
    {
        using var stream = typeof(IdentityEmailTemplateRenderer).Assembly
            .GetManifestResourceStream(LogoResourceName)
            ?? throw new InvalidOperationException("Embedded UGTM email logo was not found.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);
}
