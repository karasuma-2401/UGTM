namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations.RegisterTopic;

public sealed record RegisterTopicCommand(
    Guid TopicId,
    Guid? PartnerStudentId = null,
    string? PartnerUserCode = null);
