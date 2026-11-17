using System.Security.Cryptography;
using System.Text;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations.RegisterTopic;

public sealed class RegisterTopicHandler(
    ITopicRegistrationStore store,
    IStudentInfoReader studentInfoReader,
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

            if (string.IsNullOrWhiteSpace(partnerInfo.Email))
            {
                return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.PartnerEmailRequired);
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
            partnerInfo is not null && rawToken is not null
                ? new TopicRegistrationInvitation(rawToken, partnerInfo.Email!, partnerInfo.FullName ?? string.Empty,
                    partnerInfo.UserCode ?? string.Empty, studentAInfo.Email, studentAInfo.FullName ?? "Bạn cùng nhóm")
                : null,
            cancellationToken);
        return registerResult;
    }
}
