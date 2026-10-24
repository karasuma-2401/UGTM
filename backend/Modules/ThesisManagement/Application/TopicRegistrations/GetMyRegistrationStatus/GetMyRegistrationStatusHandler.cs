using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations.GetMyRegistrationStatus;

public sealed class GetMyRegistrationStatusHandler(
    ITopicRegistrationStore store,
    ICurrentUser currentUser)
{
    public async Task<Result<StudentRegistrationStatusDto>> HandleAsync(
        GetMyRegistrationStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<StudentRegistrationStatusDto>(
                Error.Unauthorized("Auth.Unauthorized", "Vui lòng đăng nhập để kiểm tra trạng thái đăng ký."));
        }

        var status = await store.GetStudentRegistrationStatusAsync(currentUser.UserId.Value, cancellationToken);
        if (status is null)
        {
            status = new StudentRegistrationStatusDto(
                HasRegistered: false,
                GroupId: null,
                TopicId: null,
                TopicTitleVi: null,
                ConfirmationStatus: null,
                ConfirmationExpiresAtUtc: null,
                ViolationCount: 0,
                IsRegistrationBlocked: false,
                BlockedAtUtc: null);
        }

        return Result.Success(status);
    }
}
