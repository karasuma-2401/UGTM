using System.Security.Cryptography;
using System.Text;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations.RegisterTopic;

public sealed class RegisterTopicHandler(
    ITopicRegistrationStore store,
    IStudentInfoReader studentInfoReader,
    IEmailSender emailSender,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan DefaultConfirmationWindow = TimeSpan.FromHours(24);

    public async Task<Result<TopicRegistrationResultDto>> HandleAsync(
        RegisterTopicCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<TopicRegistrationResultDto>(
                Error.Unauthorized("Auth.Unauthorized", "Vui lòng đăng nhập để thực hiện thao tác."));
        }

        if (command.TopicId == Guid.Empty)
        {
            return Result.Failure<TopicRegistrationResultDto>(
                Error.Validation("TopicRegistration.InvalidTopicId", "Mã đề tài không hợp lệ."));
        }

        var studentAId = currentUser.UserId.Value;
        var studentAInfo = await studentInfoReader.GetStudentByIdOrCodeAsync(studentAId, null, cancellationToken);
        if (studentAInfo is null)
        {
            return Result.Failure<TopicRegistrationResultDto>(
                Error.Forbidden("TopicRegistration.OnlyStudentsAllowed", "Chỉ sinh viên đang hoạt động mới có quyền đăng ký đề tài."));
        }

        Guid? partnerStudentId = null;
        StudentInfoContractDto? partnerInfo = null;

        var hasPartner = command.PartnerStudentId.HasValue || !string.IsNullOrWhiteSpace(command.PartnerUserCode);
        if (hasPartner)
        {
            partnerInfo = await studentInfoReader.GetStudentByIdOrCodeAsync(
                command.PartnerStudentId,
                command.PartnerUserCode,
                cancellationToken);

            if (partnerInfo is null)
            {
                return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.PartnerNotFound);
            }

            if (partnerInfo.Id == studentAId)
            {
                return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.CannotInviteSelf);
            }

            partnerStudentId = partnerInfo.Id;
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        string? rawToken = null;
        string? tokenHash = null;

        if (partnerStudentId.HasValue)
        {
            rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
        }

        var registerResult = await store.RegisterTopicAsync(
            command.TopicId,
            studentAId,
            partnerStudentId,
            nowUtc,
            DefaultConfirmationWindow,
            tokenHash ?? string.Empty,
            cancellationToken);

        if (!registerResult.IsSuccess)
        {
            return registerResult;
        }

        if (partnerStudentId.HasValue && partnerInfo is not null && rawToken is not null)
        {
            await TrySendEmailsAsync(
                studentAInfo,
                partnerInfo,
                rawToken,
                nowUtc.Add(DefaultConfirmationWindow),
                cancellationToken);
        }

        return registerResult;
    }

    private async Task TrySendEmailsAsync(
        StudentInfoContractDto studentAInfo,
        StudentInfoContractDto partner,
        string rawToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var studentAName = studentAInfo.FullName ?? "Bạn cùng nhóm";

            if (!string.IsNullOrWhiteSpace(partner.Email))
            {
                var inviteBody = $"""
                    <h3>Yêu cầu xác nhận tham gia đề tài khóa luận tốt nghiệp</h3>
                    <p>Xin chào <strong>{partner.FullName}</strong>,</p>
                    <p>Sinh viên <strong>{studentAName}</strong> đã đăng ký đề tài khóa luận và chỉ định bạn là thành viên cùng nhóm.</p>
                    <p>Vui lòng đăng nhập hệ thống và xác nhận tham gia hoặc sử dụng mã xác nhận bên dưới:</p>
                    <p style="font-size: 18px; font-weight: bold; color: #1a73e8; background: #e8f0fe; padding: 10px; display: inline-block;">
                        {rawToken}
                    </p>
                    <p><strong>Thời hạn xác nhận:</strong> {expiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC (trong vòng 24 giờ).</p>
                    <p><em>Lưu ý: Nếu quá hạn trên mà bạn chưa xác nhận, yêu cầu sẽ bị hủy bỏ và hệ thống sẽ ghi nhận 1 lần vi phạm cảnh cáo.</em></p>
                    """;

                await emailSender.SendAsync(
                    new EmailMessage(partner.Email, "[UGTM] Xác nhận tham gia nhóm đăng ký đề tài KLTN", inviteBody),
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(studentAInfo.Email))
            {
                var notifyBody = $"""
                    <h3>Đăng ký đề tài khóa luận thành công</h3>
                    <p>Xin chào <strong>{studentAInfo.FullName}</strong>,</p>
                    <p>Bạn đã đăng ký đề tài khóa luận thành công kèm bạn cùng nhóm <strong>{partner.FullName}</strong> ({partner.UserCode}).</p>
                    <p>Hệ thống đang tạm giữ chỗ đề tài cho nhóm của bạn. Yêu cầu tham gia đã được gửi tới email của bạn cùng nhóm và đang chờ xác nhận trước <strong>{expiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC</strong>.</p>
                    """;

                await emailSender.SendAsync(
                    new EmailMessage(studentAInfo.Email, "[UGTM] Thông báo đăng ký đề tài khóa luận", notifyBody),
                    cancellationToken);
            }
        }
        catch
        {
            // Email errors do not rollback database registration
        }
    }
}
