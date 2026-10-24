using Microsoft.Extensions.Logging;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Expiration;

namespace UGTM.Modules.ThesisManagement.Infrastructure.BackgroundServices;

public sealed class TopicRegistrationExpirationEnforcer(
    ITopicRegistrationStore store,
    IStudentInfoReader studentInfoReader,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<TopicRegistrationExpirationEnforcer> logger) : ITopicRegistrationExpirationEnforcer
{
    private const int MaxViolationsBeforeBlock = 3;

    public async Task<TopicRegistrationExpirationResult> EnforceAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var expiredMembers = await store.ExpirePendingRegistrationsAsync(
            nowUtc,
            MaxViolationsBeforeBlock,
            cancellationToken);

        if (expiredMembers.Count == 0)
        {
            return new TopicRegistrationExpirationResult(0, 0);
        }

        var studentIds = expiredMembers.Select(m => m.StudentId).Distinct().ToList();
        var students = await studentInfoReader.GetStudentsByIdsAsync(studentIds, cancellationToken);

        var blockedCount = 0;

        foreach (var expired in expiredMembers)
        {
            if (expired.IsNowBlocked)
            {
                blockedCount++;
            }

            students.TryGetValue(expired.StudentId, out var student);
            if (student is null || string.IsNullOrWhiteSpace(student.Email))
            {
                continue;
            }

            try
            {
                if (expired.IsNowBlocked)
                {
                    var blockBody = $"""
                        <h3 style="color: #d93025;">Khóa quyền đăng ký đề tài khóa luận</h3>
                        <p>Xin chào <strong>{student.FullName}</strong> ({student.UserCode}),</p>
                        <p>Tài khoản của bạn đã tích lũy <strong>{expired.NewViolationCount} lần vi phạm</strong> do để quá hạn yêu cầu xác nhận tham gia đề tài khóa luận.</p>
                        <p>Theo quy định, hệ thống đã <strong>khóa quyền đăng ký đề tài</strong> của bạn trong các đợt khóa luận tiếp theo. Bạn vẫn có thể đăng nhập để theo dõi thông tin học vụ bình thường.</p>
                        <p>Mọi thắc mắc vui lòng liên hệ Văn phòng Khoa để được giải đáp.</p>
                        """;

                    await emailSender.SendAsync(
                        new EmailMessage(student.Email, "[UGTM] Thông báo: Khóa quyền đăng ký đề tài khóa luận", blockBody),
                        cancellationToken);
                }
                else
                {
                    var warnBody = $"""
                        <h3 style="color: #e37400;">Cảnh cáo quá hạn xác nhận đăng ký đề tài ({expired.NewViolationCount}/{MaxViolationsBeforeBlock})</h3>
                        <p>Xin chào <strong>{student.FullName}</strong> ({student.UserCode}),</p>
                        <p>Bạn đã không bấm xác nhận tham gia đề tài đúng thời hạn quy định (24 giờ). Yêu cầu ghép nhóm đã bị hủy bỏ và chỗ của bạn đã được giải phóng.</p>
                        <p>Hệ thống đã ghi nhận <strong>1 lần vi phạm cảnh cáo</strong> đối với tài khoản của bạn (Hiện tại: <strong>{expired.NewViolationCount}/{MaxViolationsBeforeBlock}</strong> lần).</p>
                        <p><em>Lưu ý: Nếu vi phạm đủ {MaxViolationsBeforeBlock} lần, bạn sẽ bị khóa quyền đăng ký đề tài khóa luận.</em></p>
                        """;

                    await emailSender.SendAsync(
                        new EmailMessage(student.Email, $"[UGTM] Cảnh cáo: Quá hạn xác nhận tham gia đề tài ({expired.NewViolationCount}/{MaxViolationsBeforeBlock})", warnBody),
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send expiration email to student {StudentId}.", expired.StudentId);
            }
        }

        return new TopicRegistrationExpirationResult(expiredMembers.Count, blockedCount);
    }
}
