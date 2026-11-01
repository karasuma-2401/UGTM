using System.Net;
using Microsoft.Extensions.Options;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.Common;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Notifications;

public sealed class ThesisEmailTemplateRenderer(IOptions<ThesisEmailOptions>? configuredOptions = null) : IThesisEmailTemplateRenderer
{
    private const string BrandBlue = "#2054A5";
    private const string BrandOrange = "#FA7921";
    private const string ProductName = "Hệ thống quản lý tốt nghiệp";
    private readonly ThesisEmailOptions options = configuredOptions?.Value ?? new ThesisEmailOptions();

    public EmailMessage RenderTopicRegistrationInvite(
        string to,
        string partnerFullName,
        string studentAName,
        string rawToken,
        DateTime expiresAtUtc)
    {
        var encodedPartnerName = Html(partnerFullName);
        var encodedStudentAName = Html(studentAName);
        var encodedToken = Html(rawToken);
        var confirmUrl = ConfirmUrl(rawToken);
        var encodedConfirmUrl = Html(confirmUrl);
        var expiresFormatted = expiresAtUtc.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        var content = $$"""
            <p style="margin:0 0 10px;color:#697E9F;font-size:13px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">Xác nhận tham gia đề tài</p>
            <h1 style="margin:0 0 18px;color:#10182C;font-size:28px;line-height:1.25;font-weight:750;">Xác nhận tham gia nhóm KLTN</h1>
            <p style="margin:0 0 14px;color:#1B2940;font-size:16px;line-height:1.7;">Xin chào <strong>{{encodedPartnerName}}</strong>,</p>
            <p style="margin:0 0 14px;color:#1B2940;font-size:16px;line-height:1.7;">Sinh viên <strong>{{encodedStudentAName}}</strong> đã đăng ký đề tài khóa luận và chỉ định bạn là thành viên cùng nhóm.</p>
            <p style="margin:0 0 22px;color:#1B2940;font-size:16px;line-height:1.7;">Hãy sử dụng mã xác nhận bên dưới hoặc nhấn nút xác nhận để hoàn tất thủ tục ghép nhóm trên hệ thống:</p>
            <div style="margin:0 0 24px;padding:20px 16px;background:#F7F9FC;border:1px solid #E0E7F1;border-left:5px solid {{BrandBlue}};border-radius:10px;text-align:center;">
              <div style="margin:0 0 7px;color:#697E9F;font-size:12px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;">Mã xác nhận tham gia</div>
              <div style="color:#10182C;font-family:Consolas,'Courier New',monospace;font-size:22px;font-weight:800;letter-spacing:.08em;line-height:1.3;word-break:break-all;">{{encodedToken}}</div>
            </div>
            <table role="presentation" border="0" cellpadding="0" cellspacing="0" style="margin:0 0 26px;">
              <tr>
                <td bgcolor="{{BrandBlue}}" style="border-radius:10px;background-color:{{BrandBlue}};background-image:linear-gradient(90deg,{{BrandBlue}} 0%,{{BrandOrange}} 100%);">
                  <a href="{{encodedConfirmUrl}}" style="display:inline-block;padding:14px 28px;color:#FFFFFF;font-size:16px;font-weight:700;line-height:20px;text-decoration:none;border-radius:10px;">Xác nhận tham gia đề tài&nbsp;&nbsp;→</a>
                </td>
              </tr>
            </table>
            <p style="margin:0 0 9px;color:#697E9F;font-size:13px;line-height:1.6;">Nếu nút không hoạt động, hãy sao chép liên kết sau vào trình duyệt:</p>
            <div style="margin:0 0 26px;padding:13px 15px;background:#F7F9FC;border:1px solid #E0E7F1;border-radius:8px;color:#2054A5;font-size:12px;line-height:1.6;word-break:break-all;">{{encodedConfirmUrl}}</div>
            {{Notice("Lưu ý quan trọng", $"Thời hạn xác nhận là trước {expiresFormatted} (trong vòng 24 giờ). Nếu quá thời hạn trên mà bạn chưa xác nhận, yêu cầu ghép nhóm sẽ tự động bị hủy và hệ thống sẽ ghi nhận 1 lần vi phạm cảnh cáo.")}}
            """;

        return new EmailMessage(
            to,
            "[UGTM] Xác nhận tham gia nhóm đăng ký đề tài KLTN",
            Layout("Yêu cầu xác nhận tham gia nhóm đề tài KLTN trên hệ thống UGTM.", content),
            $"Xin chào {partnerFullName},\n\n"
            + $"Sinh viên {studentAName} đã đăng ký đề tài khóa luận và chỉ định bạn là thành viên cùng nhóm.\n\n"
            + $"Mã xác nhận tham gia của bạn là: {rawToken}\n"
            + $"Liên kết xác nhận: {confirmUrl}\n\n"
            + $"Thời hạn xác nhận: trước {expiresFormatted} (trong vòng 24 giờ).\n\n"
            + "Lưu ý quan trọng: Nếu quá thời hạn trên mà bạn chưa xác nhận, yêu cầu sẽ tự động bị hủy và hệ thống sẽ ghi nhận 1 lần vi phạm cảnh cáo.");
    }

    public EmailMessage RenderTopicRegistrationConfirmationForInviter(
        string to,
        string studentAFullName,
        string partnerFullName,
        string partnerUserCode,
        DateTime expiresAtUtc)
    {
        var encodedStudentAName = Html(studentAFullName);
        var encodedPartnerName = Html(partnerFullName);
        var encodedUserCode = Html(partnerUserCode);
        var portalUrl = PortalUrl();
        var encodedPortalUrl = Html(portalUrl);
        var expiresFormatted = expiresAtUtc.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        var content = $$"""
            <p style="margin:0 0 10px;color:#697E9F;font-size:13px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">Đăng ký đề tài khóa luận</p>
            <h1 style="margin:0 0 18px;color:#10182C;font-size:28px;line-height:1.25;font-weight:750;">Đăng ký đề tài khóa luận thành công</h1>
            <p style="margin:0 0 14px;color:#1B2940;font-size:16px;line-height:1.7;">Xin chào <strong>{{encodedStudentAName}}</strong>,</p>
            <p style="margin:0 0 18px;color:#1B2940;font-size:16px;line-height:1.7;">Bạn đã đăng ký đề tài khóa luận thành công kèm bạn cùng nhóm <strong>{{encodedPartnerName}}</strong> ({{encodedUserCode}}).</p>
            <div style="margin:0 0 26px;padding:18px 20px;background:#F7F9FC;border:1px solid #E0E7F1;border-left:5px solid {{BrandBlue}};border-radius:10px;">
              <div style="margin:0 0 6px;color:#697E9F;font-size:12px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">Trạng thái nhóm đề tài</div>
              <div style="color:#10182C;font-size:15px;line-height:1.65;">
                Hệ thống đang <strong>tạm giữ chỗ đề tài</strong> cho nhóm của bạn. Yêu cầu tham gia đã được gửi tới email của bạn cùng nhóm và đang chờ xác nhận trước <strong>{{expiresFormatted}}</strong>.
              </div>
            </div>
            <table role="presentation" border="0" cellpadding="0" cellspacing="0" style="margin:0 0 26px;">
              <tr>
                <td bgcolor="{{BrandBlue}}" style="border-radius:10px;background-color:{{BrandBlue}};background-image:linear-gradient(90deg,{{BrandBlue}} 0%,{{BrandOrange}} 100%);">
                  <a href="{{encodedPortalUrl}}" style="display:inline-block;padding:14px 28px;color:#FFFFFF;font-size:16px;font-weight:700;line-height:20px;text-decoration:none;border-radius:10px;">Kiểm tra trạng thái đề tài&nbsp;&nbsp;→</a>
                </td>
              </tr>
            </table>
            {{Notice("Lưu ý", "Vui lòng nhắc nhở bạn cùng nhóm kiểm tra hộp thư email (bao gồm cả thư mục Spam) và xác nhận tham gia đúng hạn. Nếu quá 24 giờ chưa xác nhận, đề tài sẽ tự động được mở lại cho các nhóm khác đăng ký.")}}
            """;

        return new EmailMessage(
            to,
            "[UGTM] Thông báo đăng ký đề tài khóa luận",
            Layout("Đăng ký đề tài khóa luận thành công trên hệ thống UGTM.", content),
            $"Xin chào {studentAFullName},\n\n"
            + $"Bạn đã đăng ký đề tài khóa luận thành công kèm bạn cùng nhóm {partnerFullName} ({partnerUserCode}).\n\n"
            + $"Hệ thống đang tạm giữ chỗ đề tài cho nhóm của bạn. Yêu cầu tham gia đã được gửi tới email của bạn cùng nhóm và đang chờ xác nhận trước {expiresFormatted}.\n\n"
            + "Lưu ý: Vui lòng nhắc nhở bạn cùng nhóm kiểm tra hộp thư email và xác nhận tham gia đúng hạn.");
    }

    public EmailMessage RenderAdvisorAssignedNotification(
        string to,
        string studentFullName,
        string studentUserCode)
    {
        var encodedStudentName = Html(studentFullName);
        var encodedUserCode = Html(studentUserCode);
        var portalUrl = PortalUrl();
        var encodedPortalUrl = Html(portalUrl);

        var content = $$"""
            <p style="margin:0 0 10px;color:#697E9F;font-size:13px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">Thông báo từ giảng viên hướng dẫn</p>
            <h1 style="margin:0 0 18px;color:#10182C;font-size:28px;line-height:1.25;font-weight:750;">Ghi nhận vào nhóm đề tài KLTN</h1>
            <p style="margin:0 0 14px;color:#1B2940;font-size:16px;line-height:1.7;">Xin chào <strong>{{encodedStudentName}}</strong> ({{encodedUserCode}}),</p>
            <p style="margin:0 0 18px;color:#1B2940;font-size:16px;line-height:1.7;">Giảng viên hướng dẫn đã chính thức ghi nhận bạn vào nhóm đề tài khóa luận tốt nghiệp trong giai đoạn chuẩn bị đề tài.</p>
            <div style="margin:0 0 26px;padding:18px 20px;background:#F7F9FC;border:1px solid #E0E7F1;border-left:5px solid {{BrandBlue}};border-radius:10px;">
              <div style="margin:0 0 6px;color:#697E9F;font-size:12px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">Trạng thái đề tài</div>
              <div style="color:#10182C;font-size:15px;line-height:1.65;">
                Trạng thái đăng ký của bạn đã được <strong>xác nhận chính thức (Confirmed)</strong> trên hệ thống.
              </div>
            </div>
            <table role="presentation" border="0" cellpadding="0" cellspacing="0" style="margin:0 0 26px;">
              <tr>
                <td bgcolor="{{BrandBlue}}" style="border-radius:10px;background-color:{{BrandBlue}};background-image:linear-gradient(90deg,{{BrandBlue}} 0%,{{BrandOrange}} 100%);">
                  <a href="{{encodedPortalUrl}}" style="display:inline-block;padding:14px 28px;color:#FFFFFF;font-size:16px;font-weight:700;line-height:20px;text-decoration:none;border-radius:10px;">Xem thông tin đề tài&nbsp;&nbsp;→</a>
                </td>
              </tr>
            </table>
            {{Notice("Thông tin thêm", "Vui lòng đăng nhập hệ thống UGTM để theo dõi chi tiết thông tin đề tài và tiến độ thực hiện khóa luận.")}}
            """;

        return new EmailMessage(
            to,
            "[UGTM] Thông báo gán đề tài khóa luận từ GVHD",
            Layout("Thông báo ghi nhận vào nhóm đề tài KLTN từ giảng viên hướng dẫn.", content),
            $"Xin chào {studentFullName} ({studentUserCode}),\n\n"
            + "Giảng viên hướng dẫn đã chính thức ghi nhận bạn vào nhóm đề tài khóa luận tốt nghiệp trong giai đoạn chuẩn bị đề tài.\n\n"
            + "Trạng thái đăng ký của bạn đã được xác nhận chính thức (Confirmed) trên hệ thống.\n\n"
            + "Vui lòng đăng nhập hệ thống UGTM để theo dõi chi tiết thông tin đề tài.");
    }

    public EmailMessage RenderTopicRegistrationWarning(
        string to,
        string studentFullName,
        string studentUserCode,
        int violationCount,
        int maxViolations)
    {
        var encodedStudentName = Html(studentFullName);
        var encodedUserCode = Html(studentUserCode);

        var content = $$"""
            <p style="margin:0 0 10px;color:#697E9F;font-size:13px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">Cảnh cáo vi phạm</p>
            <h1 style="margin:0 0 18px;color:#10182C;font-size:28px;line-height:1.25;font-weight:750;">Quá hạn xác nhận tham gia đề tài</h1>
            <p style="margin:0 0 14px;color:#1B2940;font-size:16px;line-height:1.7;">Xin chào <strong>{{encodedStudentName}}</strong> ({{encodedUserCode}}),</p>
            <p style="margin:0 0 18px;color:#1B2940;font-size:16px;line-height:1.7;">Bạn đã không bấm xác nhận tham gia đề tài đúng thời hạn quy định (24 giờ). Yêu cầu ghép nhóm đã bị hủy bỏ và chỗ của bạn đã được giải phóng.</p>
            {{Notice("Cảnh cáo vi phạm", $"Hệ thống đã ghi nhận 1 lần vi phạm cảnh cáo đối với tài khoản của bạn (Hiện tại: {violationCount}/{maxViolations} lần). Nếu vi phạm đủ {maxViolations} lần, bạn sẽ bị khóa quyền đăng ký đề tài khóa luận.")}}
            """;

        return new EmailMessage(
            to,
            $"[UGTM] Cảnh cáo: Quá hạn xác nhận tham gia đề tài ({violationCount}/{maxViolations})",
            Layout("Cảnh cáo vi phạm: Quá hạn xác nhận tham gia đề tài khóa luận.", content),
            $"Xin chào {studentFullName} ({studentUserCode}),\n\n"
            + "Bạn đã không bấm xác nhận tham gia đề tài đúng thời hạn quy định (24 giờ). Yêu cầu ghép nhóm đã bị hủy bỏ và chỗ của bạn đã được giải phóng.\n\n"
            + $"Hệ thống đã ghi nhận 1 lần vi phạm cảnh cáo đối với tài khoản của bạn (Hiện tại: {violationCount}/{maxViolations} lần).\n"
            + $"Lưu ý: Nếu vi phạm đủ {maxViolations} lần, bạn sẽ bị khóa quyền đăng ký đề tài khóa luận.");
    }

    public EmailMessage RenderTopicRegistrationBlocked(
        string to,
        string studentFullName,
        string studentUserCode,
        int violationCount)
    {
        var encodedStudentName = Html(studentFullName);
        var encodedUserCode = Html(studentUserCode);

        var content = $$"""
            <p style="margin:0 0 10px;color:#E02424;font-size:13px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;">Khóa quyền đăng ký</p>
            <h1 style="margin:0 0 18px;color:#10182C;font-size:28px;line-height:1.25;font-weight:750;">Khóa quyền đăng ký đề tài khóa luận</h1>
            <p style="margin:0 0 14px;color:#1B2940;font-size:16px;line-height:1.7;">Xin chào <strong>{{encodedStudentName}}</strong> ({{encodedUserCode}}),</p>
            {{Notice("Thông báo khóa quyền", $"Tài khoản của bạn đã tích lũy {violationCount} lần vi phạm do để quá hạn yêu cầu xác nhận tham gia đề tài khóa luận. Theo quy định, hệ thống đã khóa quyền đăng ký đề tài của bạn trong các đợt khóa luận tiếp theo. Bạn vẫn có thể đăng nhập để theo dõi thông tin học vụ bình thường.", "#FDF2F2", "#F8B4B4", "#9B1C1C", "#C81E1E")}}
            <p style="margin:20px 0 0;color:#697E9F;font-size:14px;line-height:1.65;">Mọi thắc mắc vui lòng liên hệ Văn phòng Khoa để được giải đáp và hỗ trợ.</p>
            """;

        return new EmailMessage(
            to,
            "[UGTM] Thông báo: Khóa quyền đăng ký đề tài khóa luận",
            Layout("Thông báo khóa quyền đăng ký đề tài khóa luận trên hệ thống UGTM.", content),
            $"Xin chào {studentFullName} ({studentUserCode}),\n\n"
            + $"Tài khoản của bạn đã tích lũy {violationCount} lần vi phạm do để quá hạn yêu cầu xác nhận tham gia đề tài khóa luận.\n\n"
            + "Theo quy định, hệ thống đã khóa quyền đăng ký đề tài của bạn trong các đợt khóa luận tiếp theo. Bạn vẫn có thể đăng nhập để theo dõi thông tin học vụ bình thường.\n\n"
            + "Mọi thắc mắc vui lòng liên hệ Văn phòng Khoa để được giải đáp.");
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

    private static string Notice(
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
                  <strong style="color:{{titleColor}};">{{title}}:</strong> {{message}}
                </td>
              </tr>
            </table>
            """;
    }

    private string ConfirmUrl(string token)
    {
        var builder = new UriBuilder(new Uri(options.PublicWebBaseUrl, "/topics/confirm"))
        {
            Fragment = $"token={Uri.EscapeDataString(token)}"
        };

        return builder.Uri.AbsoluteUri;
    }

    private string PortalUrl(string path = "/topics")
    {
        var builder = new UriBuilder(new Uri(options.PublicWebBaseUrl, path));
        return builder.Uri.AbsoluteUri;
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);
}
