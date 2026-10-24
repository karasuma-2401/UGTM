using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations;

public interface ITopicRegistrationStore
{
    Task<Result<TopicRegistrationResultDto>> RegisterTopicAsync(
        Guid topicId,
        Guid studentAId,
        Guid? studentBId,
        DateTime nowUtc,
        TimeSpan confirmationWindow,
        string confirmationTokenHash,
        CancellationToken cancellationToken = default);

    Task<Result<ConfirmRegistrationResultDto>> ConfirmRegistrationAsync(
        string tokenHash,
        Guid? currentStudentId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpiredPendingMemberDto>> ExpirePendingRegistrationsAsync(
        DateTime nowUtc,
        int maxViolationsBeforeBlock,
        CancellationToken cancellationToken = default);

    Task<StudentRegistrationStatusDto?> GetStudentRegistrationStatusAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);
}
