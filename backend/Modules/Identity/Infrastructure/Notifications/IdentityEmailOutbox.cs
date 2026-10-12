using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Infrastructure.Persistence;

namespace UGTM.Modules.Identity.Infrastructure.Notifications;

public sealed record IdentityEmailPayload(
    string To,
    string FullName,
    string Template,
    string Secret);

public sealed class IdentityEmailOutbox(
    IdentityDbContext db,
    IDataProtectionProvider dataProtection) : IIdentityEmailOutbox
{
    public const string ProtectionPurpose = "UGTM.Identity.EmailOutbox.v1";
    private readonly IDataProtector _protector =
        dataProtection.CreateProtector(ProtectionPurpose);

    public void EnqueueActivationEmail(
        Guid userId, string email, string fullName, string rawToken, DateTime createdAt)
        => Add(new IdentityEmailPayload(email, fullName, "activation", rawToken), createdAt);

    public void EnqueuePasswordRecoveryEmail(
        Guid userId, string email, string fullName, string code, DateTime createdAt)
        => Add(new IdentityEmailPayload(email, fullName, "recovery-otp", code), createdAt);

    private void Add(IdentityEmailPayload payload, DateTime createdAt)
    {
        var json = JsonSerializer.Serialize(payload);
        db.IdentityOutboxMessages.Add(IdentityOutboxMessage.Create(
            payload.Template,
            _protector.Protect(json),
            ProtectionPurpose,
            createdAt));
    }
}