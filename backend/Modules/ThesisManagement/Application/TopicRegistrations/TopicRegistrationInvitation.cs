namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations;

public sealed record TopicRegistrationInvitation(
    string RawToken,
    string PartnerEmail,
    string PartnerName,
    string PartnerUserCode,
    string? InviterEmail,
    string InviterName);

public interface ITopicRegistrationInvitationOutbox
{
    void Enqueue(Guid memberId, TopicRegistrationInvitation invitation, TimeSpan confirmationWindow, DateTime createdAtUtc);
}

public interface ITopicRegistrationEmailDispatcher
{
    Task<bool> ProcessOneAsync(CancellationToken cancellationToken = default);
}

public interface IAdvisorAssignmentEmailOutbox
{
    void Enqueue(Guid memberId, string email, string fullName, string userCode, DateTime createdAtUtc);
}
