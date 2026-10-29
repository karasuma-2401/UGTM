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
    TimeProvider timeProvider,
    IThesisEmailTemplateRenderer emailTemplateRenderer)
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
                var message = emailTemplateRenderer.RenderTopicRegistrationInvite(
                    partner.Email,
                    partner.FullName ?? string.Empty,
                    studentAName,
                    rawToken,
                    expiresAtUtc);

                await emailSender.SendAsync(message, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(studentAInfo.Email))
            {
                var message = emailTemplateRenderer.RenderTopicRegistrationConfirmationForInviter(
                    studentAInfo.Email,
                    studentAInfo.FullName ?? string.Empty,
                    partner.FullName ?? string.Empty,
                    partner.UserCode ?? string.Empty,
                    expiresAtUtc);

                await emailSender.SendAsync(message, cancellationToken);
            }
        }
        catch
        {
            // Email errors do not rollback database registration
        }
    }
}
