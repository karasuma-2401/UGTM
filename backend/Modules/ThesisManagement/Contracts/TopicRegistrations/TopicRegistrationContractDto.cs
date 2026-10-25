namespace UGTM.Modules.ThesisManagement.Contracts.TopicRegistrations;

public sealed record TopicMemberContractDto(
    Guid StudentId,
    Guid BatchId,
    DateTime JoinedAt);

public sealed record TopicRegistrationContractDto(
    Guid GroupId,
    Guid TopicId,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<TopicMemberContractDto> Members);

public sealed record AdvisorStudentAssignmentInputDto(
    Guid? StudentId = null,
    string? UserCode = null);

public sealed record AdvisorAssignStudentsContractResultDto(
    bool IsSuccess,
    string? ErrorCode,
    string? ErrorMessage,
    Guid? GroupId,
    Guid TopicId,
    Guid BatchId,
    IReadOnlyList<Guid> AssignedStudentIds)
{
    public static AdvisorAssignStudentsContractResultDto Success(
        Guid? groupId,
        Guid topicId,
        Guid batchId,
        IReadOnlyList<Guid> assignedStudentIds)
        => new(true, null, null, groupId, topicId, batchId, assignedStudentIds);

    public static AdvisorAssignStudentsContractResultDto Failure(string errorCode, string errorMessage)
        => new(false, errorCode, errorMessage, null, Guid.Empty, Guid.Empty, Array.Empty<Guid>());
}

public sealed record AdvisorUnassignStudentsContractResultDto(
    bool IsSuccess,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static AdvisorUnassignStudentsContractResultDto Success()
        => new(true, null, null);

    public static AdvisorUnassignStudentsContractResultDto Failure(string errorCode, string errorMessage)
        => new(false, errorCode, errorMessage);
}

public interface IThesisRegistrationContract
{
    Task<IReadOnlyDictionary<Guid, TopicRegistrationContractDto>> GetRegistrationsByTopicIdsAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default);

    Task<TopicRegistrationContractDto?> GetRegistrationByTopicIdAsync(
        Guid topicId,
        CancellationToken cancellationToken = default);

    Task<AdvisorAssignStudentsContractResultDto> AssignStudentsByAdvisorAsync(
        Guid topicId,
        Guid actorUserId,
        bool isAdmin,
        IReadOnlyCollection<AdvisorStudentAssignmentInputDto> students,
        CancellationToken cancellationToken = default);

    Task<AdvisorUnassignStudentsContractResultDto> UnassignStudentsByAdvisorAsync(
        Guid topicId,
        Guid actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
