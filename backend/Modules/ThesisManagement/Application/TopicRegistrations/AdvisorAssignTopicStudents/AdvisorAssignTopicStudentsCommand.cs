namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations.AdvisorAssignTopicStudents;

public sealed record AdvisorAssignTopicStudentsCommand(
    Guid TopicId,
    IReadOnlyCollection<Guid>? StudentIds = null,
    IReadOnlyCollection<string>? StudentCodes = null);
