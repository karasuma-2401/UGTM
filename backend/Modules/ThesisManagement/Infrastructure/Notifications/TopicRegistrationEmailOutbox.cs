using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Notifications;

internal sealed record TopicRegistrationEmailPayload(
    string To, string RecipientName, string PartnerName, string PartnerUserCode,
    string? RawToken, string? InviterEmail, string InviterName, int WindowSeconds, DateTime? DeadlineUtc);

internal sealed class TopicRegistrationEmailOutbox(
    ThesisManagementDbContext db,
    IDataProtectionProvider dataProtection,
    IOptions<ThesisEmailOptions> options) : ITopicRegistrationInvitationOutbox, IAdvisorAssignmentEmailOutbox
{
    internal const string InviteType = "Invite";
    internal const string InviterType = "Inviter";
    internal const string AdvisorAssignedType = "AdvisorAssigned";
    private const string PurposePrefix = "UGTM.ThesisManagement.TopicRegistrationEmail.v1";

    internal string Purpose => PurposeFor(options.Value.OutboxPartition);

    internal static string PurposeFor(string partition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partition);
        return $"{PurposePrefix}:{partition.Trim()}";
    }

    public void Enqueue(Guid memberId, TopicRegistrationInvitation invitation, TimeSpan confirmationWindow, DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        if (memberId == Guid.Empty || confirmationWindow <= TimeSpan.Zero || confirmationWindow.TotalSeconds > int.MaxValue)
            throw new ArgumentException("Invitation and confirmation window must be valid.");
        Add(memberId, InviteType, new TopicRegistrationEmailPayload(
            invitation.PartnerEmail, invitation.PartnerName, invitation.PartnerName, invitation.PartnerUserCode,
            invitation.RawToken, invitation.InviterEmail, invitation.InviterName,
            checked((int)confirmationWindow.TotalSeconds), null), createdAtUtc);
    }

    public void Enqueue(Guid memberId, string email, string fullName, string userCode, DateTime createdAtUtc)
        => Add(memberId, AdvisorAssignedType,
            new TopicRegistrationEmailPayload(email, fullName, string.Empty, userCode, null, null, string.Empty, 0, null),
            createdAtUtc);

    internal void Add(Guid memberId, string type, TopicRegistrationEmailPayload payload, DateTime nowUtc)
    {
        var purpose = Purpose;
        db.TopicRegistrationEmails.Add(new TopicRegistrationEmail
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Type = type,
            ProtectionPurpose = purpose,
            ProtectedPayload = dataProtection.CreateProtector(purpose).Protect(JsonSerializer.Serialize(payload)),
            CreatedAtUtc = nowUtc,
            NextAttemptAtUtc = nowUtc
        });
    }
}
