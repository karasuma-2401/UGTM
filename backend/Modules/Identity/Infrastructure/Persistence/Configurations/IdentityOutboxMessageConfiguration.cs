using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class IdentityOutboxMessageConfiguration
    : IEntityTypeConfiguration<IdentityOutboxMessage>
{
    public void Configure(EntityTypeBuilder<IdentityOutboxMessage> builder)
    {
        builder.ToTable("outbox_message", "identity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProtectedPayload).HasColumnType("text").IsRequired();
        builder.Property(x => x.ProtectionPurpose).HasMaxLength(200).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt });
    }
}